# 5-Sprint Implementation Plan: The "Digital Twin" Ecosystem

**Architecture:** Clean Architecture (Domain → Application → Infrastructure → Presentation), SOLID Principles, Dependency Injection, Event-Driven.

**Strategy:** Frontend-First, Mock-Driven Development → Real Implementation.

**Diagrams:** See `diagrams/` folder:
- `database.puml` — PlantUML ER diagram of the database schema
- `c4-context.puml` — C4 Level 1: System Context
- `c4-domain.puml` — C4 Level 3: Domain layer components
- `c4-application.puml` — C4 Level 3: Application layer components
- `c4-infrastructure.puml` — C4 Level 3: Infrastructure layer components
- `c4-presentation.puml` — C4 Level 3: Presentation layer components

---

## Clean Architecture Layer Structure

```
IOSHealthApp/
├── IOSHealthApp.Domain/           # Layer 1: Business logic, no external dependencies
│   ├── Models/                    # Domain models (business entities)
│   ├── Services/                  # Business logic, domain rules
│   ├── Interfaces/                # Repository & provider contracts (implemented elsewhere)
│   ├── Validators/                # FluentValidation: domain model validation
│   ├── Enums/
│   └── Exceptions/
│
├── IOSHealthApp.Application/      # Layer 2: Orchestration, DTOs, mapping
│   ├── DTOs/                      # Data transfer objects for MAUI & API
│   ├── Mappers/                   # Map: Domain Models ↔ DTOs ↔ MAUI ViewModels
│   ├── Validators/                # FluentValidation: DTO and command validation
│   ├── Interfaces/                # Application-level contracts
│   └── Services/                  # Application services (orchestrate Domain + Infrastructure)
│
├── IOSHealthApp.Infrastructure/   # Layer 3: Database only
│   ├── Data/                      # DbContext, EF Core configuration
│   ├── Entities/                  # EF entities (database tables)
│   ├── Repositories/              # EF repository implementations
│   └── Migrations/
│
└── IOSHealthApp/                  # Layer 4: Presentation (MAUI Blazor)
    ├── Components/
    ├── Pages/
    ├── Layout/
    ├── Integrations/              # HealthKit, HTTP APIs, SignalR (platform/external adapters)
    └── MauiProgram.cs
```

### Layer Responsibilities

| Layer | Responsibility | Contains |
|-------|----------------|----------|
| **Domain** | Business logic, rules, domain models | `Models` (VitalSign, PatientProfile, etc.), `Services` (VitalSignService, MedicationInteractionService), `Interfaces` (IRepository, IHealthDataProvider) |
| **Application** | DTOs, mapping between layers, orchestration | `DTOs`, `Mappers` (Domain↔DTO↔MAUI), `Services` (call Domain + coordinate data flow) |
| **Infrastructure** | Database persistence only | `DbContext`, EF `Entities`, `Repositories` (implement Domain's IRepository) |
| **Presentation (IOSHealthApp)** | UI, MAUI Blazor | Pages, Components, `Integrations` (HealthKit, APIs, SignalR—external adapters) |

### Data Flow

```
MAUI (ViewModels/DTOs) ←→ Application (Mappers, DTOs) ←→ Domain (Models, Services)
                                                              ↑
Infrastructure (EF Entities, DbContext) ────────────────────┘
        Maps: EF Entity ↔ Domain Model (in Repository)
```

- **Domain models** = pure business objects (no EF attributes).
- **EF entities** = database tables; Infrastructure maps EF ↔ Domain in repositories.
- **DTOs** = used by MAUI and Application; Application mappers convert Domain models ↔ DTOs.
- **Domain services** = contain business logic; use `IRepository<T>` and other interfaces.
- **Infrastructure** = only database; no HealthKit, HTTP, or external APIs.
- **FluentValidation** = Domain validates models; Application validates DTOs and commands.

---

## Sprint 1: Foundation, Clean Architecture & Mock-Driven UI

**Duration:** 2 weeks  
**Goal:** Establish Clean Architecture, migrate existing code into layers, build medical-grade UI shell with mock data.

---

### 1.1 Project Structure & Domain Layer

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.1.1 | Create solution structure | Add `IOSHealthApp.Domain`, `IOSHealthApp.Application`, `IOSHealthApp.Infrastructure` class libraries. Reference: Application→Domain, Infrastructure→Domain+Application, IOSHealthApp→Application+Infrastructure. | `IOS app.sln`, `*.csproj` |
| 1.1.2 | Domain Models | Create `PatientProfile`, `VitalSign`, `EnvironmentReading`, `Medication` in `Domain/Models/`. Pure POCOs, no EF attributes. Add `SleepSession` (placeholder). | `Domain/Models/*.cs` |
| 1.1.3 | Domain Interfaces | Add `IVitalSignRepository`, `IHealthDataProvider`, `IEnvironmentDataProvider`, `ICoachingProvider` in `Domain/Interfaces/`. Domain defines contracts; implementations live in Infrastructure (repos) or Presentation/Integrations (providers). | `Domain/Interfaces/*.cs` |
| 1.1.4 | Domain Services | Create `VitalSignService`, `EnvironmentAssessmentService` in `Domain/Services/`. Business logic: e.g., compute trend, determine air quality level from PM2.5. | `Domain/Services/VitalSignService.cs`, `EnvironmentAssessmentService.cs` |
| 1.1.5 | Domain Exceptions | Create `DomainException` base. Add `InvalidVitalSignException`, `HealthDataUnavailableException`. | `Domain/Exceptions/DomainException.cs` |
| 1.1.6 | FluentValidation (Domain) | Add `FluentValidation` package to Domain. Create `VitalSignValidator`, `PatientProfileValidator`, `EnvironmentReadingValidator`, `MedicationValidator`. Validate: VitalSign (Type in allowed set, Value in range, Unit non-empty); PatientProfile (FullName required, DateOfBirth valid); EnvironmentReading (PM2.5 ≥ 0, Temp in range); Medication (Name, Dosage required). | `Domain/Validators/VitalSignValidator.cs`, etc. |

**Acceptance Criteria:** Domain has zero package references except `System` and `FluentValidation`. No EF, no HTTP. Business logic in Domain services. Domain models validated before use.

---

### 1.2 Application Layer (DTOs & Mappers)

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.2.1 | Create DTOs | Add `VitalSignDto`, `EnvironmentReadingDto`, `CoachingAdviceDto`. These are used by MAUI for binding. | `Application/DTOs/VitalSignDto.cs`, `EnvironmentReadingDto.cs`, `CoachingAdviceDto.cs` |
| 1.2.2 | Mappers: Domain → DTO | Create `VitalSignMapper.ToDto(VitalSign model)`, `EnvironmentReadingMapper.ToDto(EnvironmentReading model)`. Map Domain models to DTOs for MAUI. | `Application/Mappers/VitalSignMapper.cs`, `EnvironmentReadingMapper.cs` |
| 1.2.3 | Mappers: DTO → Domain | Create reverse mappers where needed: `VitalSignMapper.ToModel(VitalSignDto dto)` for user input → Domain. | `Application/Mappers/*` |
| 1.2.4 | Application Services | Create `VitalsApplicationService`, `EnvironmentApplicationService`. These orchestrate: call Domain services, call providers (injected from Presentation), map Domain → DTO. Expose `IObservable<VitalSignDto> GetLiveVitals()`, `Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync()`. | `Application/Services/VitalsApplicationService.cs`, `EnvironmentApplicationService.cs` |
| 1.2.5 | Application Interfaces | Add `IVitalsApplicationService`, `IEnvironmentApplicationService` in `Application/Interfaces/`. MAUI depends on these. | `Application/Interfaces/*.cs` |
| 1.2.6 | FluentValidation (Application) | Add `FluentValidation` and `FluentValidation.DependencyInjectionExtensions` to Application. Create `VitalSignDtoValidator`, `EnvironmentReadingDtoValidator`, `CoachingAdviceDtoValidator`. Validate DTOs before mapping to Domain or sending to UI. Register validators with `AddValidatorsFromAssemblyContaining<>()`. Application services call `await _validator.ValidateAndThrowAsync(dto)` before processing. | `Application/Validators/*.cs`, `Application/DependencyInjection.cs` |

**Acceptance Criteria:** Application has DTOs and mappers. DTOs validated with FluentValidation before use. Application services orchestrate Domain + data providers. No UI references.

---

### 1.3 Infrastructure Layer (Database & EF Entities Only)

*Schema: See `diagrams/database.puml` — User (not PatientProfile), Patient, UserOAuth (Google), DoctorPatientAssignment, Medication (past/future), MedicalDocument, Appointment.*

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.3.1 | EF Entities | Create EF entities per `database.puml`: `UserEntity`, `UserOAuthEntity`, `PatientEntity`, `DoctorPatientAssignmentEntity`, `VitalSignEntity`, `MedicationEntity`, `EnvironmentReadingEntity`, etc. Enums as int. Soft delete: `DeletedAt`. | `Infrastructure/Entities/*.cs` |
| 1.3.2 | DbContext | Create `HealthAppDbContext` in `Infrastructure/Data/`. Configure `DbSet<VitalSignEntity>`, etc. Add EF Core package. | `Infrastructure/Data/HealthAppDbContext.cs` |
| 1.3.3 | Repositories | Create `VitalSignRepository` implementing `IVitalSignRepository` (from Domain). Map EF ↔ Domain model in repository. | `Infrastructure/Repositories/VitalSignRepository.cs` |
| 1.3.4 | DI Registration | Create `AddInfrastructure(this IServiceCollection)`. Register `DbContext`, `IVitalSignRepository`. | `Infrastructure/DependencyInjection.cs` |
| 1.3.5 | Initial Migration | Run `dotnet ef migrations add Initial`. | `Infrastructure/Migrations/*` |

**Acceptance Criteria:** Infrastructure only contains database, EF entities, repositories. No HealthKit, HTTP, or mocks.

---

### 1.4 Integrations (Presentation Layer – Data Providers)

Data providers (HealthKit, APIs, mocks) live in `IOSHealthApp/Integrations/`—not in Infrastructure.

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.4.1 | Mock Health Provider | Create `Integrations/Mocks/MockHealthProvider.cs` implementing `IHealthDataProvider` (Domain). Generate sine-wave HR, SpO2, Steps, Calories. | `IOSHealthApp/Integrations/Mocks/MockHealthProvider.cs` |
| 1.4.2 | Mock Environment Provider | Create `MockEnvironmentProvider.cs` implementing `IEnvironmentDataProvider`. Return randomized PM2.5, Temperature, Humidity. | `IOSHealthApp/Integrations/Mocks/MockEnvironmentProvider.cs` |
| 1.4.3 | Mock Coaching Provider | Create `MockCoachingProvider.cs` implementing `ICoachingProvider`. Return canned advice. | `IOSHealthApp/Integrations/Mocks/MockCoachingProvider.cs` |
| 1.4.4 | DI Registration | In `MauiProgram.cs`, register mock providers with `AddScoped`. Application services receive them via constructor injection. | `MauiProgram.cs` |

**Acceptance Criteria:** Mocks live in Presentation. Domain interfaces implemented by Integrations. Swap mocks for HealthKit/HTTP in Sprint 2.

---

### 1.5 Presentation Layer – Medical Theme & Components

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.5.1 | MudBlazor Medical Theme | Create `Components/Theme/MedicalTheme.razor` or `MedicalTheme.cs`. Primary: Medical Teal `#009688`. Secondary: Heartbeat Red `#E91E63`. Dark mode default. Typography: medical-grade readability. | `Components/Theme/MedicalTheme.razor` or `.cs` |
| 1.5.2 | MetricCard Component | Ensure `MetricCard` accepts `Title`, `Value`, `Unit`, `Trend`, `SparklineData`, `LastUpdated`, `AccentColor`. Add `Decimals` parameter. Use SVG sparkline. | `Components/Shared/MetricCard.razor` |
| 1.5.3 | DigitalTwinMannequin Component | Create SVG body outline. Highlight regions: Heart, Lungs, Activity. Add CSS animation for heartbeat (pulse) driven by `HeartRate` prop. | `Components/Shared/DigitalTwinMannequin.razor` |
| 1.5.4 | Environment Badge Component | Create `EnvironmentBadge.razor`. Display "Good" (green), "Moderate" (amber), "Unhealthy" (red) based on `AirQualityLevel`. | `Components/Shared/EnvironmentBadge.razor` |

**Acceptance Criteria:** Theme applied app-wide. MetricCard and DigitalTwinMannequin render correctly. EnvironmentBadge shows correct colors.

---

### 1.6 Feature Implementation – Wearables Dashboard & Environment Widget

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 1.6.1 | Home Page – Wearables Dashboard | Inject `IVitalsApplicationService`. Subscribe to `GetLiveVitals()` with `IDisposable`. Bind 4 `MetricCard`s: Heart Rate, SpO2, Steps, Calories. Compute trend from last 2 values. Compute sparkline from last 20 samples. | `Components/Pages/Home.razor` |
| 1.6.2 | Home Page – Digital Twin | Pass `HeartRate` from live stream to `DigitalTwinMannequin`. Animate heartbeat. | `Components/Pages/Home.razor` |
| 1.6.3 | Environment Widget | Inject `IEnvironmentApplicationService`. Poll every 30s or use observable. Display Temperature, Humidity, PM2.5. Use `EnvironmentBadge`. | `Components/Pages/Home.razor` or `Components/Pages/Environment.razor` |
| 1.6.4 | MauiProgram DI | Register Application (`AddApplication()`), Infrastructure (`AddInfrastructure()`), Integrations (mock providers). | `MauiProgram.cs`, `Application/DependencyInjection.cs` |

**Acceptance Criteria:** App runs. Dashboard shows live-updating vitals. Environment widget shows air quality badge. No static managers; all via DI.

---

### Sprint 1 Definition of Done

- [ ] Solution builds with 4 projects (Domain, Application, Infrastructure, IOSHealthApp).
- [ ] Domain: Models, Services, Interfaces. Infrastructure: DB, EF entities, Repositories only.
- [ ] Integrations (Mocks) in Presentation. Application: DTOs, Mappers, Application Services.
- [ ] Wearables dashboard displays Heart Rate, SpO2, Steps, Calories with sparklines.
- [ ] Environment widget displays PM2.5, Temperature, Air Quality badge.
- [ ] Digital Twin mannequin animates with heart rate.
- [ ] Unit tests for at least 2 Domain services.

---

## Sprint 2: Data Ingestion & External Connectivity

**Duration:** 2 weeks  
**Goal:** Replace mocks with real adapters. Integrate HealthKit, OpenWeatherMap, Google Air Quality, RxNav.

---

### 2.1 HealthKit Integration (iOS)

HealthKit lives in `IOSHealthApp/Integrations/` (Presentation), not Infrastructure.

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 2.1.1 | Add HealthKit NuGet | Add `Microsoft.Maui.HealthKit` or equivalent. Create `Platforms/iOS/Info.plist` entries for `NSHealthShareUsageDescription`, `NSHealthUpdateUsageDescription`. | `IOSHealthApp.csproj`, `Platforms/iOS/Info.plist` |
| 2.1.2 | HealthKit Provider | Create `Integrations/HealthKit/HealthKitProvider.cs` implementing `IHealthDataProvider` (Domain). Map `HKQuantityTypeIdentifierHeartRate` → Domain `VitalSign`. Map StepCount, OxygenSaturation. | `IOSHealthApp/Integrations/HealthKit/HealthKitProvider.cs` |
| 2.1.3 | HealthKit Permissions | Implement permission request flow. Handle denied/restricted. Expose `Task<bool> RequestPermissionsAsync()`. | `HealthKitProvider.cs` |
| 2.1.4 | Background Fetch (iOS) | Register `BGAppRefreshTaskRequest` for hourly sync. On callback, call `HealthKitProvider` to fetch latest samples and update local cache. | `Platforms/iOS/BackgroundFetchHandler.cs` |
| 2.1.5 | Platform-Specific Registration | Use `#if IOS` in DI to register `HealthKitProvider` on iOS, `MockHealthProvider` on Android/Windows. | `MauiProgram.cs` |

**Acceptance Criteria:** On iOS device/simulator, real HealthKit data flows to dashboard. Background fetch runs when app is closed.

---

### 2.2 Environment APIs (OpenWeatherMap + Google Air Quality)

Environment APIs live in `Integrations/` (Presentation). Domain service evaluates risk.

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 2.2.1 | HTTP Client Factory | Add `IHttpClientFactory` to IOSHealthApp. Create named client `"EnvironmentApi"` with base URL and timeout. | `MauiProgram.cs` or `Integrations/Environment/` |
| 2.2.2 | OpenWeatherMap Provider | Create `Integrations/Environment/OpenWeatherMapProvider.cs` implementing `IEnvironmentDataProvider`. Call `GET /data/2.5/weather`. Map to Domain `EnvironmentReading`. | `IOSHealthApp/Integrations/Environment/OpenWeatherMapProvider.cs` |
| 2.2.3 | Google Air Quality Provider | Create `GoogleAirQualityProvider`. Call Google Air Quality API. Map UAQI to `AirQualityLevel`. | `IOSHealthApp/Integrations/Environment/GoogleAirQualityProvider.cs` |
| 2.2.4 | Composite Provider | Create `HttpEnvironmentProvider` that aggregates OpenWeatherMap + Google Air Quality. Returns Domain `EnvironmentReading`. | `IOSHealthApp/Integrations/Environment/HttpEnvironmentProvider.cs` |
| 2.2.5 | Risk Event (Domain) | In `Domain/Services/EnvironmentAssessmentService`, if `AirQualityLevel == Unhealthy`, raise `RiskEvent`. Application/UI subscribes and shows toast. | `Domain/Services/EnvironmentAssessmentService.cs`, `Domain/Events/RiskEvent.cs` |

**Acceptance Criteria:** Environment widget shows real weather and air quality. Poor air quality triggers Domain risk event.

---

### 2.3 Pharmacological Safety (RxNav)

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 2.3.1 | Domain Model | Add `MedicationInteraction` to `Domain/Models/`: `DrugA`, `DrugB`, `Severity` (High/Medium/Low/N/A), `Description`. | `Domain/Models/MedicationInteraction.cs` |
| 2.3.2 | Domain Service | Create `MedicationInteractionService` in Domain. Business logic: `EvaluateSeverity()`, `HasHighRisk()` etc. | `Domain/Services/MedicationInteractionService.cs` |
| 2.3.3 | Domain Interface | Add `IMedicationInteractionProvider` in Domain. Returns raw interaction data from external source. | `Domain/Interfaces/IMedicationInteractionProvider.cs` |
| 2.3.4 | RxNav Provider (Integrations) | Create `Integrations/Medication/RxNavProvider.cs` implementing `IMedicationInteractionProvider`. Call `GET .../interaction/list.json?rxcuis={list}`. Parse JSON, return Domain models. | `IOSHealthApp/Integrations/Medication/RxNavProvider.cs` |
| 2.3.5 | Application DTO & Mapper | Add `MedicationInteractionDto`. Application service maps Domain → DTO for MAUI. | `Application/DTOs/`, `Application/Mappers/` |
| 2.3.6 | MedicationSafetyBadge & Page | Create `MedicationSafetyBadge.razor`. Red if `Severity == High`. Create `Medications.razor` page. | `Components/Shared/MedicationSafetyBadge.razor`, `Components/Pages/Medications.razor` |

**Acceptance Criteria:** Domain has interaction logic. RxNav in Integrations. Badge turns red when high-severity interaction exists.

---

### Sprint 2 Definition of Done

- [ ] HealthKit provides real vitals on iOS.
- [ ] Environment widget uses OpenWeatherMap + Google Air Quality APIs.
- [ ] RxNav integration returns interaction data.
- [ ] MedicationSafetyBadge displays correctly.
- [ ] Configuration (API keys) in `appsettings.json` or environment variables; never committed.

---

## Sprint 3: IoT Hardware & Signal Processing

**Duration:** 2–3 weeks  
**Goal:** ESP32 ECG/SpO2 pipeline, SignalR backend, high-performance ECG visualization, triage rule engine.

---

### 3.1 ESP32 Firmware & Edge Processing

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 3.1.1 | ESP32 Project Setup | Create `Esp32Firmware/` (PlatformIO or Arduino). Configure AD8232 on Analog Pin 34, MAX30102 on I2C. | `Esp32Firmware/platformio.ini`, `main.cpp` |
| 3.1.2 | AD8232 Sampling | Sample at 500 Hz. Buffer 1 second (500 samples). Apply 50 Hz notch filter (IIR or FIR) to remove electrical noise. | `Esp32Firmware/ecg_sampler.cpp` |
| 3.1.3 | MAX30102 SpO2 | Read SpO2 and HR from MAX30102. Output every 1–2 seconds. | `Esp32Firmware/spo2_reader.cpp` |
| 3.1.4 | MQTT/SignalR Client | Send payloads: `{ "ecg": [int16...], "spo2": float, "hr": int, "ts": long }`. Use MQTT or WebSocket to backend. | `Esp32Firmware/transport.cpp` |

**Acceptance Criteria:** ESP32 sends ECG + SpO2 data to backend. 50 Hz noise removed in firmware.

---

### 3.2 Backend Signal Hub

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 3.2.1 | ASP.NET Core Hub | Create `ECGStreamHub : Hub`. Method `BroadcastPacket(byte[] compressedData)`. Clients subscribe to receive. | `Backend/Hubs/ECGStreamHub.cs` (separate project or same) |
| 3.2.2 | Domain Model | Add `EcgFrame` in Domain: `double[] Samples`, `double SpO2`, `int HeartRate`, `DateTime Timestamp`. | `Domain/Models/EcgFrame.cs` |
| 3.2.3 | Domain Interface | Add `IEcgStreamProvider` in Domain. | `Domain/Interfaces/IEcgStreamProvider.cs` |
| 3.2.4 | SignalR Client (Integrations) | Create `Integrations/SignalR/EcgStreamClient.cs` implementing `IEcgStreamProvider`. Connect to hub, decompress, emit `IObservable<EcgFrame>`. | `IOSHealthApp/Integrations/SignalR/EcgStreamClient.cs` |
| 3.2.5 | Application DTO | Add `EcgFrameDto` for MAUI binding. Mapper: Domain `EcgFrame` ↔ DTO. | `Application/DTOs/EcgFrameDto.cs` |

**Acceptance Criteria:** MAUI app receives real-time ECG frames from backend when ESP32 is connected.

---

### 3.3 High-Performance ECG Visualization

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 3.3.1 | SkiaSharp Canvas | Add `SkiaSharp.Views.Maui`. Create `EcgCanvasView` custom control. Draw ECG waveform. | `Components/Controls/EcgCanvasView.cs` |
| 3.3.2 | Sweep Effect | Implement rolling buffer (e.g., 5 seconds at 500 Hz = 2500 points). As new data arrives, shift left and append. Clear old data (sweep like hospital monitor). | `EcgCanvasView.cs` |
| 3.3.3 | ECG Page | Create `EcgMonitor.razor` or XAML page. Subscribe to `IEcgStreamClient`. Pass samples to `EcgCanvasView`. Display SpO2 and HR from frame. | `Components/Pages/EcgMonitor.razor` |

**Acceptance Criteria:** ECG renders at 500 pts/s without lag. Sweep effect visible. SpO2 and HR displayed.

---

### 3.4 Triage Rule Engine (Domain Business Logic)

Triage rules are business logic → Domain.

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 3.4.1 | Domain: Rule Interface | Create `IEcgTriageRule` in Domain: `TriageResult Evaluate(EcgFrame frame)`. `TriageResult`: Pass, Warn, Critical. | `Domain/Interfaces/IEcgTriageRule.cs` |
| 3.4.2 | Domain: SignalQualityRule | Implement `SignalQualityRule` in `Domain/Services/Triage/`. Check flatline/noise. Fail → Critical. | `Domain/Services/Triage/SignalQualityRule.cs` |
| 3.4.3 | Domain: HeartRateActivityRule | Implement `HeartRateActivityRule`. If HR &gt; 150 and Activity == Resting → Critical. | `Domain/Services/Triage/HeartRateActivityRule.cs` |
| 3.4.4 | Domain: SpO2Rule | Implement `SpO2Rule`. If SpO2 &lt; 90% → Critical. | `Domain/Services/Triage/SpO2Rule.cs` |
| 3.4.5 | Domain: EcgTriageEngine | Create `EcgTriageEngine` in Domain. Chain rules. On first Critical, raise `CriticalAlertEvent`. | `Domain/Services/Triage/EcgTriageEngine.cs` |
| 3.4.6 | Critical Alert UI | Show modal or banner when `CriticalAlertEvent` fires. | `Components/Shared/CriticalAlertBanner.razor` |

**Acceptance Criteria:** Triage logic in Domain. Critical alerts surface in UI.

---

### Sprint 3 Definition of Done

- [ ] ESP32 sends ECG + SpO2 to backend.
- [ ] MAUI app displays live ECG with sweep effect.
- [ ] Triage rules run on each frame.
- [ ] Critical alerts displayed to user.

---

## Sprint 4: AI & Digital Twin Intelligence

**Duration:** 2–3 weeks  
**Goal:** CNN anomaly detection, RAG medical assistant, behavioral coaching with Gemini.

---

### 4.1 CNN Anomaly Detection

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 4.1.1 | Python API Service | Create FastAPI/Flask service. Load 1D-CNN model (TensorFlow/PyTorch) trained on MIT-BIH. Endpoint: `POST /predict` accepts 10-second ECG strip (5000 samples). Returns `{ "class": "N"|"V"|"A", "confidence": float }`. | `PythonApi/main.py`, `model/` |
| 4.1.2 | Domain Model & Interface | Add `AnomalyPrediction` in Domain. Add `IAnomalyDetectionProvider` in Domain. | `Domain/Models/AnomalyPrediction.cs`, `Domain/Interfaces/IAnomalyDetectionProvider.cs` |
| 4.1.3 | HTTP Provider (Integrations) | Create `Integrations/AI/HttpAnomalyDetectionProvider.cs` implementing `IAnomalyDetectionProvider`. POST to Python API. | `IOSHealthApp/Integrations/AI/HttpAnomalyDetectionProvider.cs` |
| 4.1.4 | Application DTO & Service | Add `AnomalyPredictionDto`. Application service orchestrates Domain + provider, maps to DTO. | `Application/DTOs/`, `Application/Services/` |
| 4.1.5 | Integration in ECG Flow | After receiving 10s of ECG, call Application service. Display "Normal" / "PVC" / "AFib" badge. | `Components/Pages/EcgMonitor.razor` |

**Acceptance Criteria:** 10s ECG strip sent to Python API. Result displayed in app.

---

### 4.2 RAG Medical Assistant

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 4.2.1 | Vector DB Setup | Setup Qdrant or Pinecone. Ingest PDF chunks (e.g., ESC AF Guidelines). Create embeddings (OpenAI/Cohere). | `RagService/ingest.py` |
| 4.2.2 | Domain Interface | Add `IMedicalAssistantProvider` in Domain: `Task<string> AskAsync(string question, CancellationToken ct)`. | `Domain/Interfaces/IMedicalAssistantProvider.cs` |
| 4.2.3 | RAG Provider (Integrations) | Create `Integrations/AI/RagMedicalAssistantProvider.cs`. 1) Embed question. 2) Retrieve chunks. 3) Build prompt. 4) Call GPT-4/Llama. Return answer. | `IOSHealthApp/Integrations/AI/RagMedicalAssistantProvider.cs` |
| 4.2.4 | Application Service | Add `IMedicalAssistantApplicationService`. Orchestrates provider, returns DTO/string for MAUI. | `Application/Interfaces/`, `Application/Services/` |
| 4.2.5 | ChatBotWindow & Page | Create `ChatBotWindow.razor`, `MedicalAssistant.razor`. Call Application service. | `Components/Shared/ChatBotWindow.razor`, `Components/Pages/MedicalAssistant.razor` |

**Acceptance Criteria:** User asks "Why is my heart skipping a beat?" and receives context-grounded answer.

---

### 4.3 Behavioral Coaching (Gemini Pro)

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 4.3.1 | Domain Interface | `ICoachingProvider` in Domain (from Sprint 1). Input: Steps, Avg HR, Sleep Score. Output: Natural language advice. | `Domain/Interfaces/ICoachingProvider.cs` |
| 4.3.2 | Gemini Provider (Integrations) | Create `Integrations/AI/GeminiCoachingProvider.cs` implementing `ICoachingProvider`. Call Gemini Pro API: "User walked {steps} steps (Low), avg HR {hr}, sleep score {sleep}. Suggest recovery plan." | `IOSHealthApp/Integrations/AI/GeminiCoachingProvider.cs` |
| 4.3.3 | Replace Mock in DI | Register `GeminiCoachingProvider` instead of `MockCoachingProvider` when API key configured. | `MauiProgram.cs` |
| 4.3.4 | Coaching Widget | Display advice on Home or dedicated Coaching page. Refresh on demand or daily. | `Components/Pages/Home.razor` or `Coaching.razor` |

**Acceptance Criteria:** Coaching returns personalized advice from Gemini based on user data.

---

### Sprint 4 Definition of Done

- [ ] CNN model returns N/V/A classification for 10s ECG.
- [ ] RAG assistant answers medical questions from indexed guidelines.
- [ ] Gemini provides behavioral coaching.

---

## Sprint 5: Ecosystem Closure & Production Hardening

**Duration:** 2 weeks  
**Goal:** OCR for discharge letters, doctor portal, reporting, CI/CD, TestFlight/Play Console.

---

### 5.1 OCR & Document Ingestion

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 5.1.1 | Domain Interface & Model | Add `IOcrProvider` in Domain. Add `OcrResult` model (key-value pairs: Diagnosis, Meds, etc.). | `Domain/Interfaces/IOcrProvider.cs`, `Domain/Models/OcrResult.cs` |
| 5.1.2 | Domain: FHIR Mapping Logic | Create `DischargeLetterMappingService` in Domain. Business logic: map extracted strings to Domain models (Medication, PatientProfile). | `Domain/Services/DischargeLetterMappingService.cs` |
| 5.1.3 | Azure Provider (Integrations) | Create `Integrations/Ocr/AzureFormRecognizerProvider.cs` implementing `IOcrProvider`. Use Document Intelligence API. | `IOSHealthApp/Integrations/Ocr/AzureFormRecognizerProvider.cs` |
| 5.1.4 | Application Service | Orchestrate: call OCR provider → Domain mapping service → save via Repository (Infrastructure). | `Application/Services/DocumentImportApplicationService.cs` |
| 5.1.5 | Document Upload UI | Create `Documents.razor`. File picker → OCR → show extracted data → user confirms → save. | `Components/Pages/Documents.razor` |

**Acceptance Criteria:** User uploads discharge letter PDF. Domain maps to medications. Data persisted via Infrastructure (DB).

---

### 5.2 Doctor Portal & Ticketing

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 5.2.1 | DoctorMainLayout | Create `DoctorMainLayout.razor`. Different nav: Patient List, Actions, Reports. | `Components/Layout/DoctorMainLayout.razor` |
| 5.2.2 | Domain: RiskScore | Add `RiskScore` calculation in Domain service (e.g., from alert count). | `Domain/Services/PatientRiskService.cs` |
| 5.2.3 | Infrastructure: Doctor Actions | Add `DoctorActionEntity` EF entity. Repository for CRUD. Doctor actions stored in DB. | `Infrastructure/Entities/DoctorActionEntity.cs`, `Infrastructure/Repositories/` |
| 5.2.4 | Push Notifications (Integrations) | Integrate FCM (Android) / APNs (iOS) in `Integrations/Notifications/`. Handle "doctor_action" notification type. | `IOSHealthApp/Integrations/Notifications/` |
| 5.2.5 | Patient List View | Sort by `RiskScore`. Display patient cards. Doctor creates action → save to DB → trigger push. | `Components/Pages/Doctor/PatientList.razor` |

**Acceptance Criteria:** Doctor can view patients by risk. Doctor can create action. Patient receives push. Data in Infrastructure (DB).

---

### 5.3 Reporting & Export

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 5.3.1 | Domain: Report Model | Add `HealthReport` model in Domain (Summary, ArrhythmiaBurden, EcgStrips). Domain service aggregates data from repositories. | `Domain/Models/HealthReport.cs`, `Domain/Services/ReportAggregationService.cs` |
| 5.3.2 | Domain Interface | Add `IReportExporter` in Domain: `Task<Stream> ExportToPdfAsync(HealthReport report)`. | `Domain/Interfaces/IReportExporter.cs` |
| 5.3.3 | QuestPDF (Integrations) | Create `Integrations/Reporting/QuestPdfReportExporter.cs` implementing `IReportExporter`. Page 1: Summary. Page 2: Arrhythmia burden. Page 3: ECG strips. | `IOSHealthApp/Integrations/Reporting/QuestPdfReportExporter.cs` |
| 5.3.4 | Application Service | Orchestrate: Domain aggregates report from DB → call exporter → return stream. | `Application/Services/ReportApplicationService.cs` |
| 5.3.5 | Export UI | Button "Export PDF" on Reports page. Generate and share/save file. | `Components/Pages/Reports.razor` |

**Acceptance Criteria:** PDF report generated. Data from Infrastructure (DB). Export logic in Integrations.

---

### 5.4 CI/CD & Production

| Task ID | Task | Details | Files to Create/Modify |
|---------|------|---------|------------------------|
| 5.4.1 | Unit Tests | Achieve 80%+ coverage on Domain, Application use cases, Infrastructure adapters (with mocked HTTP). | `*Tests/` projects |
| 5.4.2 | CI Pipeline | GitHub Actions or Azure DevOps: build, test, pack. | `.github/workflows/ci.yml` |
| 5.4.3 | TestFlight / Play Console | Configure signing. Build release. Upload to TestFlight (iOS) and Play Console (Android). | `ios/ExportOptions.plist`, `android/keystore` |
| 5.4.4 | Secrets Management | API keys in Azure Key Vault or environment variables. Never in repo. | `appsettings.Production.json` (no secrets) |

**Acceptance Criteria:** CI runs on push. App deployable to TestFlight and Play Console. No secrets in source.

---

### Sprint 5 Definition of Done

- [ ] OCR extracts data from discharge letters.
- [ ] Doctor portal with patient list and ticketing.
- [ ] PDF report generation works.
- [ ] 80%+ unit test coverage.
- [ ] CI/CD deploys to TestFlight/Play Console.

---

## Summary: Clean Architecture Migration Checklist

| Sprint | Domain | Application | Infrastructure | Presentation (IOSHealthApp) |
|--------|--------|-------------|----------------|-----------------------------|
| 1 | Models, Services, Interfaces, Validators, Exceptions | DTOs, Mappers, Validators, Application Services | DbContext, EF Entities, Repositories | Integrations/Mocks, Theme, MetricCard, DigitalTwin, Home |
| 2 | MedicationInteraction model, MedicationInteractionService | Medication DTOs, Mappers | — (DB only) | Integrations: HealthKit, HttpEnvironment, RxNav; Medications page |
| 3 | EcgFrame model, Triage rules (EcgTriageEngine) | EcgFrameDto, Mappers | — | Integrations: SignalR; EcgCanvasView, EcgMonitor, CriticalAlertBanner |
| 4 | AnomalyPrediction, IAnomalyDetectionProvider, IMedicalAssistantProvider, ICoachingProvider | DTOs, Application Services | — | Integrations: CNN API, RAG, Gemini; ChatBotWindow, MedicalAssistant |
| 5 | OcrResult, DischargeLetterMappingService, HealthReport, IReportExporter | DocumentImport, ReportApplicationService | DoctorActionEntity, Repositories | Integrations: Azure OCR, QuestPDF, Push; Documents, DoctorMainLayout, Reports |

---

## Definition of Done (All Sprints)

- Code committed to version control.
- Unit tests pass.
- No new linter/analyzer warnings.
- Documentation updated for new APIs.
- Sprint demo completed.
