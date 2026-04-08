#include <stdlib.h>
#include <string.h>
// Minimal stubs to satisfy the Swift @_silgen_name linker symbols when the
// real embedded .NET native library is not linked (e.g. simulator builds).
//
// These return JSON strings matching the Swift decoders' expectations.

static char* dup_cstr(const char* s) {
  if (s == NULL) return NULL;
  size_t n = strlen(s);
  char* out = (char*)malloc(n + 1);
  if (!out) return NULL;
  memcpy(out, s, n);
  out[n] = '\0';
  return out;
}

static const char* kNotLinkedJson = "{\"success\":false,\"error\":\".NET native engine not linked for this build (stub).\"}";
static const char* kNotLinkedAuthJson = "{\"success\":false,\"errorMessage\":\".NET native engine not linked for this build (stub).\",\"accessToken\":null,\"user\":null}";

const char* mobile_engine_initialize(const char* databasePath, const char* apiBaseUrl) {
  (void)databasePath; (void)apiBaseUrl;
  return dup_cstr(kNotLinkedJson);
}

const char* mobile_engine_initialize_database(void) { return dup_cstr(kNotLinkedJson); }
void mobile_engine_dispose(void) { }

const char* mobile_engine_authenticate(const char* googleIdToken) {
  (void)googleIdToken;
  return dup_cstr(kNotLinkedAuthJson);
}

const char* mobile_engine_get_current_user(void) { return dup_cstr("null"); }
const char* mobile_engine_get_patient_profile(void) { return dup_cstr("null"); }
const char* mobile_engine_update_patient_profile(const char* updateJson) {
  (void)updateJson;
  return dup_cstr(kNotLinkedJson);
}

const char* mobile_engine_record_vital_sign(const char* vitalSignJson) {
  (void)vitalSignJson;
  return dup_cstr(kNotLinkedJson);
}

const char* mobile_engine_record_vital_signs(const char* vitalSignsJson) {
  (void)vitalSignsJson;
  return dup_cstr("{\"success\":false,\"count\":0,\"error\":\".NET native engine not linked for this build (stub).\"}");
}

const char* mobile_engine_get_vital_signs(const char* fromDateIso, const char* toDateIso) {
  (void)fromDateIso; (void)toDateIso;
  return dup_cstr("[]");
}

const char* mobile_engine_get_vital_signs_by_type(int vitalTypeInt, const char* fromDateIso, const char* toDateIso) {
  (void)vitalTypeInt; (void)fromDateIso; (void)toDateIso;
  return dup_cstr("[]");
}

const char* mobile_engine_perform_sync(void) { return dup_cstr(kNotLinkedJson); }
const char* mobile_engine_push_local_changes(void) { return dup_cstr(kNotLinkedJson); }

void mobile_engine_free_string(const char* ptr) {
  if (ptr) free((void*)ptr);
}

