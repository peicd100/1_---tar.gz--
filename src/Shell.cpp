#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <string>
#include <new>

// {D67DBF63-9748-4E72-B7C8-8D17C5BD9377}; kept in sync with AppxManifest.xml.
static const CLSID CommandId = {0xd67dbf63,0x9748,0x4e72,{0xb7,0xc8,0x8d,0x17,0xc5,0xbd,0x93,0x77}};
static HMODULE module;
static LONG objects = 0;
static HRESULT Copy(const std::wstring& s, LPWSTR* out) {
    if (!out) return E_POINTER;
    *out = static_cast<LPWSTR>(CoTaskMemAlloc((s.size()+1)*sizeof(wchar_t)));
    if (!*out) return E_OUTOFMEMORY;
    CopyMemory(*out, s.c_str(), (s.size()+1)*sizeof(wchar_t)); return S_OK;
}
static std::wstring Folder() {
    wchar_t path[32768]; DWORD n = GetModuleFileNameW(module, path, 32768);
    if (!n || n >= 32768) return L"";
    std::wstring s(path); return s.substr(0, s.find_last_of(L"\\")+1);
}
class Command : public IExplorerCommand {
    LONG refs = 1;
public:
    Command() { InterlockedIncrement(&objects); }
    ~Command() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** out) override {
        if (!out) return E_POINTER; *out = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IExplorerCommand)) {
            *out = static_cast<IExplorerCommand*>(this); AddRef(); return S_OK;
        } return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { LONG n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, LPWSTR* out) override { return Copy(L"壓縮成 tar.gz", out); }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, LPWSTR* out) override { return Copy(Folder()+L"TarGz.exe,0", out); }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, LPWSTR* out) override { return Copy(L"將選取項目合併成一個 tar.gz", out); }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* id) override { if (!id) return E_POINTER; *id = CommandId; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override {
        if (!state) return E_POINTER;
        DWORD n = 0; *state = ECS_HIDDEN;
        if (items && SUCCEEDED(items->GetCount(&n)) && n) *state = ECS_ENABLED;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items, IBindCtx*) override {
        if (!items) return E_INVALIDARG;
        DWORD count = 0; HRESULT hr = items->GetCount(&count);
        if (FAILED(hr) || !count) return E_INVALIDARG;
        std::wstring contents = L"\xFEFF";
        for (DWORD i = 0; i < count; ++i) {
            IShellItem* item = nullptr; LPWSTR path = nullptr;
            hr = items->GetItemAt(i, &item);
            if (FAILED(hr)) return hr;
            hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path); item->Release();
            if (FAILED(hr)) return hr;
            contents += path; contents += L"\r\n"; CoTaskMemFree(path);
        }
        wchar_t temp[32768]; DWORD len = GetTempPathW(32768, temp);
        if (!len || len >= 32768) return E_FAIL;
        GUID id; if (FAILED(CoCreateGuid(&id))) return E_FAIL;
        wchar_t guid[40]; StringFromGUID2(id, guid, 40);
        std::wstring manifest = std::wstring(temp)+L"peicd-targz-"+guid+L".txt";
        HANDLE file = CreateFileW(manifest.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (file == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
        DWORD bytes = 0; BOOL ok = WriteFile(file, contents.data(), static_cast<DWORD>(contents.size()*2), &bytes, nullptr);
        DWORD err = GetLastError(); CloseHandle(file);
        if (!ok || bytes != contents.size()*2) { DeleteFileW(manifest.c_str()); return HRESULT_FROM_WIN32(ok ? ERROR_WRITE_FAULT : err); }
        std::wstring exe = Folder()+L"TarGz.exe";
        std::wstring cmd = L"\""+exe+L"\" --manifest \""+manifest+L"\"";
        STARTUPINFOW startup = {}; startup.cb = sizeof(startup); PROCESS_INFORMATION process = {};
        ok = CreateProcessW(exe.c_str(), &cmd[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process);
        if (!ok) { err = GetLastError(); DeleteFileW(manifest.c_str()); return HRESULT_FROM_WIN32(err); }
        CloseHandle(process.hThread); CloseHandle(process.hProcess); return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags) override { if (!flags) return E_POINTER; *flags = ECF_DEFAULT; return S_OK; }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** commands) override { if (commands) *commands = nullptr; return E_NOTIMPL; }
};
class Factory : public IClassFactory {
    LONG refs = 1;
public:
    Factory() { InterlockedIncrement(&objects); }
    ~Factory() { InterlockedDecrement(&objects); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** out) override {
        if (!out) return E_POINTER; *out = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IClassFactory)) { *out = static_cast<IClassFactory*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&refs); }
    ULONG STDMETHODCALLTYPE Release() override { LONG n = InterlockedDecrement(&refs); if (!n) delete this; return n; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** out) override {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto c = new(std::nothrow) Command; if (!c) return E_OUTOFMEMORY;
        HRESULT hr = c->QueryInterface(iid, out); c->Release(); return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) InterlockedIncrement(&objects); else InterlockedDecrement(&objects); return S_OK; }
};
extern "C" __declspec(dllexport) HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** out) {
    if (!IsEqualCLSID(clsid, CommandId)) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new(std::nothrow) Factory; if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(iid, out); factory->Release(); return hr;
}
extern "C" __declspec(dllexport) HRESULT __stdcall DllCanUnloadNow() { return objects == 0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) { module = instance; DisableThreadLibraryCalls(instance); } return TRUE;
}
