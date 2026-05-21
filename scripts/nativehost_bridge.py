import ctypes
import json
from typing import Any


class NativeHostBridge:
    def __init__(self, dylib_path: str):
        self.lib = ctypes.cdll.LoadLibrary(dylib_path)
        # functions that return allocated UTF8 pointers
        self._set_sig("mobile_engine_initialize", 7)
        self._set_sig("mobile_engine_build_structured_document_json", 1)
        # free function
        try:
            self.lib.mobile_engine_free_string.argtypes = [ctypes.c_void_p]
            self.lib.mobile_engine_free_string.restype = None
        except Exception:
            pass

    def _set_sig(self, name: str, argc: int):
        fn = getattr(self.lib, name)
        fn.argtypes = [ctypes.c_char_p] * argc
        fn.restype = ctypes.c_void_p

    def _call_and_free(self, fn_name: str, *args: str) -> str:
        fn = getattr(self.lib, fn_name)
        bargs = [a.encode("utf-8") if a is not None else b"" for a in args]
        ptr = fn(*bargs)
        if not ptr:
            return ""
        s = ctypes.string_at(ptr).decode("utf-8")
        try:
            self.lib.mobile_engine_free_string(ptr)
        except Exception:
            pass
        return s

    def initialize(self, database_path: str, api_base_url: str = "", gemini_api_key: str = "",
                   open_weather_api_key: str = "", google_oauth_client_id: str = "",
                   open_router_api_key: str = "", open_router_model: str = "") -> dict[str, Any]:
        res = self._call_and_free(
            "mobile_engine_initialize",
            database_path or "",
            api_base_url or "",
            gemini_api_key or "",
            open_weather_api_key or "",
            google_oauth_client_id or "",
            open_router_api_key or "",
            open_router_model or "",
        )
        try:
            return json.loads(res)
        except Exception:
            return {"raw": res}

    def build_structured_document_json(self, input_json: dict) -> dict:
        j = json.dumps(input_json, ensure_ascii=False)
        res = self._call_and_free("mobile_engine_build_structured_document_json", j)
        try:
            return json.loads(res)
        except Exception:
            return {"raw": res}
