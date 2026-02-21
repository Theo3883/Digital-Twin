# **Ce este aplicația**

Este un ecosistem digital complet pentru monitorizarea proactivă și continuă a sănătății cardiace, care funcționează ca o punte inteligentă între pacient și medic. Scopul ei este să creeze un „geamăn digital” al pacientului care nu doar detectează anomalii înainte să devină urgențe, ci și simplifică gestionarea istoricului medical, mutând îngrijirea din spital în confortul vieții de zi cu zi.

# **Ce face**

Aplicația automatizează întregul proces medical: de la **digitalizarea instantanee a dosarului medical** (prin scanarea documentelor vechi la înregistrare), la colectarea continuă de date vitale prin senzori IoT. Ea combină aceste informații cu factori de mediu și date despre medicamentație, folosind Inteligența Artificială pentru a oferi pacientului sfaturi personalizate, iar în situații critice, **alertează automat medicul** pentru o intervenție rapidă.

# **Principalele funcționalități (Features)**

### 1. Monitorizare Hardware (IoT) & Senzori

- **Achiziție Date ECG:** Utilizarea senzorului **AD8232** pentru monitorizarea activității electrice a inimii (detectare derivații, filtrare zgomot).
- **Parametri Vitali:** Măsurarea saturației de oxigen (SpO2) și a pulsului cu senzorul **MAX30102**.
- **Edge Computing:** Procesarea preliminară a datelor pe microcontrolerul **ESP32** înainte de transmisia Wi-Fi.

### 2. Contextualizare cu Factori de Mediu & Farmacologici

- **Monitorizare Calitate Aer:** Integrare cu API-uri externe (Google Air Quality / OpenWeatherMap) pentru corelarea crizelor cardiace cu nivelul de poluare (PM2.5).
- **Siguranță Medicamentoasă:** Verificarea automată a interacțiunilor dintre medicamente (Drug-Drug Interactions) prin API-ul **RxNav**, prevenind combinațiile periculoase.

### 3. Analiză AI Avansată & Asistent Virtual

- **"Geamăn Digital" (Digital Twin):** Crearea unui profil virtual al pacientului actualizat în timp real.
- **LLM Medical (RAG):** Asistent bazat pe **GPT-4** sau **Llama 3** care "citește" studii din PubMed și explică pacientului diagnosticele pe înțelesul său.
- **Detecție Anomalii:** Algoritmi de Machine Learning antrenați să identifice tipare de aritmie sau fibrilație atrială.
- **Raportare Stratificată (Multi-Level Reporting):**
    - Motorul AI generează interpretarea datelor pe două niveluri de complexitate distincte, selectabile din interfață:
        1. **Nivelul Pacient (Limbaj Natural Simplificat):** Explică diagnosticul folosind analogii, termeni non-tehnici și coduri de culoare (ex: *„Ritmul inimii este ușor neregulat, similar cu momentele de efort, dar fără risc imediat. Recomandare: Odihnă”*).
        2. **Nivelul Clinic (Terminologie Medicală Avansată):** Generează un raport tehnic detaliat pentru cardiolog, incluzând morfologia undei P, intervalul QT, clasificarea aritmiilor conform ghidurilor standard și referințe bibliografice relevante.

### 4. Platformă Web & Vizualizare

- **Dashboard Live:** Vizualizare EKG în timp real pentru medici, cu latență minimă (folosind **SignalR** / WebSockets).
- **Istoric & Rapoarte:** Generarea automată de rapoarte PDF pentru vizitele medicale.

### 5. Onboarding Automatizat & Procesare Documente Medicale

- **Parsare Documente prin Templetizare:**
    - La crearea contului, utilizatorul încarcă fotografii sau PDF-uri cu analizele vechi, scrisori medicale sau rețete.
    - Sistemul folosește un motor **OCR + NLP** antrenat pe șabloane medicale (templates). *(Vb si cu Irimia → doctoratul)*
- **Extragere Structurată a Datelor:**
    - Identifică automat câmpuri cheie: *Nume Pacient, Data, Diagnostic (cod ICD-10), Valori Analize, Medicamentație Curentă*.
    - Transformă textul nestructurat în date structurate (JSON/FHIR) și le populează automat în profilul pacientului, eliminând introducerea manuală a datelor.

### 6.  Sistem Proactiv de Alertare și Triaj Automat

- **Detecție Evenimente Critice:**
    - Algoritmul AI rulează o verificare în timp real asupra fiecărui set de măsurători primit. Dacă identifică anomalii severe (ex: fibrilație atrială, tahicardie ventriculară sau desaturare sub 90%), marchează evenimentul ca „Urgență”.
- **Notificări Automate către Medic:**
    - Sistemul declanșează instantaneu un **serviciu de alertare** (folosind servicii precum *SendGrid* pentru email sau *Firebase Cloud Messaging* pentru notificări push).
    - Medicul primește un email/notificare care conține:
        1. **Sumarul Alertei:** (ex: "Pacientul X - Posibil episod de Fibrilație Atrială").
        2. **Snapshot de Date:** Un grafic static cu EKG-ul din momentul crizei.
        3. **Link Securizat:** Acces direct în platformă pentru a vedea detaliile complete ale episodului.
- **Avertizare Pacient:**
    - Simultan, pacientul primește o notificare pe telefon sau ceas (dacă este conectat) cu recomandări imediate (ex: "Opriți efortul fizic", "Luați poziție de repaus").

### 7. Ecosistem Colaborativ Inter-Disciplinar (Medic Familie – Specialist)

- **Acces Partajat și Sincronizat (Shared Care Plan):**
    - Permite conectarea simultană a **Medicului de Familie** (pentru o viziune holistică) și a **Medicului Cardiolog** (pentru analiză de specialitate) la dosarul digital al pacientului.
    - Modificările făcute de un medic (ex: ajustarea dozei de medicament) sunt vizibile instantaneu pentru celălalt, prevenind deciziile contradictorii.
- **Flux de Lucru și Recomandări (Workflow):**
    - Medicul poate marca anumite evenimente direct pe grafic și poate emite „tichete” de acțiune: *„Necesită reconsult fizic”*, *„Ajustare tratament”* sau *„Monitorizare extinsă”*.
    - Pacientul vede statutul recomandării în aplicație (ex: „Medicul X recomandă o programare pentru săptămâna viitoare”).

### **8. Integrare Wearables Consumer & Coaching AI Personalizat**

- **Punte de Date (Data Bridge - Mi Fitness/Apple Health):**
    - Aplicația acționează ca un agregator central, sincronizând datele din dispozitivele purtabile comerciale (ex: Xiaomi Watch/Mi Band) prin intermediul **Apple HealthKit**.
    - Preluare parametri extinși de stil de viață: *Activitate Fizică* (pași, calorii arse, ore de stat în picioare), *Semne Vitale* (tendințe puls repaus vs. activ) și *Jurnale de Antrenament* (running, gym).
- **Motor Analitic Comportamental (Gemini Pro):**
    - Datele nu sunt doar afișate, ci procesate de un backend .NET conectat la modelul **Gemini Pro** pentru analiză semantică.
    - **Analiză de Performanță:** Evaluare comparativă (ex: "Recuperarea ritmului cardiac a fost cu 15% mai rapidă astăzi față de media săptămânii trecute").
    - **Coaching Proactiv:** Sugestii contextuale bazate pe istoricul recent (ex: "Având în vedere activitatea intensă de ieri și numărul scăzut de pași de azi, recomand o sesiune de recuperare ușoară de 20 de minute").
    - **Interogări în Limbaj Natural (NLP):** Interfață conversațională unde pacientul poate întreba liber (ex: "Cum a progresat condiția mea fizică luna aceasta?"), primind un raport textual detaliat în loc de simple grafice.