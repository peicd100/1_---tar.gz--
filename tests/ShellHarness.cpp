#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <iostream>

int wmain(int argc, wchar_t** argv) {
    if (argc < 5) return 2;
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) return 3;
    HMODULE dll = LoadLibraryW(argv[1]); if (!dll) return 4;
    auto get = reinterpret_cast<HRESULT (WINAPI*)(REFCLSID,REFIID,void**)>(GetProcAddress(dll,"DllGetClassObject"));
    auto unload = reinterpret_cast<HRESULT (WINAPI*)()>(GetProcAddress(dll,"DllCanUnloadNow"));
    if (!get || !unload || unload() != S_OK) return 5;
    CLSID cls; CLSIDFromString(L"{D67DBF63-9748-4E72-B7C8-8D17C5BD9377}", &cls);
    IClassFactory* factory = nullptr; hr = get(cls, IID_IClassFactory, reinterpret_cast<void**>(&factory)); if (FAILED(hr)) return 6;
    IExplorerCommand* command = nullptr;
    hr = factory->CreateInstance(nullptr, IID_IExplorerCommand, reinterpret_cast<void**>(&command)); factory->Release(); if (FAILED(hr)) return 7;
    LPWSTR title = nullptr; hr = command->GetTitle(nullptr, &title); if (FAILED(hr) || std::wstring(title) != L"壓縮成 tar.gz") return 8; CoTaskMemFree(title);
    EXPCMDSTATE state; command->GetState(nullptr, FALSE, &state); if (state != ECS_HIDDEN) return 9;
    PIDLIST_ABSOLUTE ids[2] = {};
    for (int i=0;i<2;i++) if (FAILED(SHParseDisplayName(argv[i+2],nullptr,&ids[i],0,nullptr))) return 10;
    IShellItemArray* items = nullptr;
    hr = SHCreateShellItemArrayFromIDLists(2, const_cast<PCIDLIST_ABSOLUTE*>(ids), &items); if (FAILED(hr)) return 11;
    command->GetState(items, FALSE, &state); if (state != ECS_ENABLED) return 12;
    SetEnvironmentVariableW(L"PEICD_TEST_REPORT", argv[4]);
    hr = command->Invoke(items, nullptr);
    items->Release(); for (auto id: ids) CoTaskMemFree(id); command->Release();
    if (FAILED(hr)) { std::cerr << std::hex << hr; return 13; }
    if (unload() != S_OK) return 14;
    FreeLibrary(dll); CoUninitialize();
    std::cout << "Shell COM title/state/multi-select/invoke/refcount PASS\n";
    return 0;
}
