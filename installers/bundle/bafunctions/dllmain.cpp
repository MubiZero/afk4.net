// AFK4.NET BAFunctions - DLL entry points consumed by WixStdBA.
// Mirrors the WiX bafunctions sample, with the correct (two-argument)
// BAFunctionsDestroy signature that WixStdBA actually calls.

#include "precomp.h"

static HINSTANCE vhInstance = NULL;

extern "C" BOOL WINAPI DllMain(
    __in HINSTANCE hInstance,
    __in DWORD dwReason,
    __in LPVOID /*pvReserved*/
    )
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        ::DisableThreadLibraryCalls(hInstance);
        vhInstance = hInstance;
        break;

    case DLL_PROCESS_DETACH:
        vhInstance = NULL;
        break;
    }

    return TRUE;
}

extern "C" HRESULT WINAPI BAFunctionsCreate(
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    )
{
    HRESULT hr = S_OK;

    BalInitialize(pArgs->pEngine);

    hr = CreateBAFunctions(vhInstance, pArgs, pResults);
    BalExitOnFailure(hr, "Failed to create BAFunctions interface.");

LExit:
    return hr;
}

extern "C" void WINAPI BAFunctionsDestroy(
    __in const BA_FUNCTIONS_DESTROY_ARGS* /*pArgs*/,
    __inout BA_FUNCTIONS_DESTROY_RESULTS* /*pResults*/
    )
{
    BalUninitialize();
}
