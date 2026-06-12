// AFK4.NET BAFunctions - turns the WixStdBA "click Install, then click Close"
// experience into a hands-off one for the interactive (full UI) case:
//
//   * OnThemeControlLoaded - remember the Install button's window handle.
//   * OnDetectComplete      - simulate a click on it, so the install starts on
//                             its own. This drives the exact same code path a
//                             real click would (and that path is already
//                             VM-verified), so WixStdBA plans + auto-applies.
//   * OnApplyComplete       - on a clean success with no pending reboot, close
//                             the window so the user never sees/needs "Close".
//
// We only touch the full-UI case. Passive/quiet runs (e.g. mass-deploy via
// `msiexec /qn` or a `/passive` bundle) already auto-install and auto-close in
// WixStdBA, and have no Install button loaded, so the guards below skip them.

#include "precomp.h"
#include "BalBaseBAFunctions.h"
#include "BalBaseBAFunctionsProc.h"

class CAfkBaFunctions : public CBalBaseBAFunctions
{
public: // IBAFunctions
    virtual STDMETHODIMP OnThemeControlLoaded(
        __in LPCWSTR wzName,
        __in WORD /*wId*/,
        __in HWND hWnd,
        __inout BOOL* /*pfProcessed*/
        )
    {
        if (CSTR_EQUAL == ::CompareStringOrdinal(wzName, -1, L"InstallButton", -1, FALSE))
        {
            m_hwndInstallButton = hWnd;
        }

        return S_OK;
    }

public: // IBootstrapperApplication
    virtual STDMETHODIMP OnDetectComplete(
        __in HRESULT hrStatus,
        __in BOOL /*fEligibleForCleanup*/
        )
    {
        if (SUCCEEDED(hrStatus) && BOOTSTRAPPER_DISPLAY_FULL == m_display && m_hwndInstallButton)
        {
            // Post (not send): let WixStdBA finish showing/enabling the install
            // page first; the queued click is then dispatched on the UI thread.
            ::PostMessageW(m_hwndInstallButton, BM_CLICK, 0, 0);
            BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "AFK4 BAFunctions: auto-started install (simulated Install click).");
        }

        return S_OK;
    }

    virtual STDMETHODIMP OnApplyComplete(
        __in HRESULT hrStatus,
        __in BOOTSTRAPPER_APPLY_RESTART restart,
        __in BOOTSTRAPPER_APPLYCOMPLETE_ACTION recommendation,
        __inout BOOTSTRAPPER_APPLYCOMPLETE_ACTION* pAction
        )
    {
        *pAction = recommendation;

        // Leave the window open on failure so the error stays readable, and
        // don't auto-close when a reboot is pending (the user must decide).
        if (SUCCEEDED(hrStatus) && BOOTSTRAPPER_APPLY_RESTART_NONE == restart &&
            BOOTSTRAPPER_DISPLAY_FULL == m_display && m_hwndParent)
        {
            ::PostMessageW(m_hwndParent, WM_CLOSE, 0, 0);
            BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "AFK4 BAFunctions: install succeeded, auto-closing the window.");
        }

        return S_OK;
    }

public:
    void SetCommand(
        __in_opt const BOOTSTRAPPER_COMMAND* pCommand
        )
    {
        if (pCommand)
        {
            m_display = pCommand->display;
        }
    }

    CAfkBaFunctions(
        __in HMODULE hModule
        ) : CBalBaseBAFunctions(hModule)
    {
        m_hwndInstallButton = NULL;
        m_display = BOOTSTRAPPER_DISPLAY_UNKNOWN;
    }

    ~CAfkBaFunctions()
    {
    }

private:
    HWND m_hwndInstallButton;
    BOOTSTRAPPER_DISPLAY m_display;
};

HRESULT WINAPI CreateBAFunctions(
    __in HMODULE hModule,
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    )
{
    HRESULT hr = S_OK;
    CAfkBaFunctions* pBAFunctions = NULL;

    pBAFunctions = new CAfkBaFunctions(hModule);
    ExitOnNull(pBAFunctions, hr, E_OUTOFMEMORY, "Failed to create new CAfkBaFunctions object.");

    hr = pBAFunctions->OnCreate(pArgs->pEngine, pArgs->pCommand);
    ExitOnFailure(hr, "Failed to call OnCreate on CAfkBaFunctions.");

    pBAFunctions->SetCommand(pArgs->pCommand);

    pResults->pfnBAFunctionsProc = BalBaseBAFunctionsProc;
    pResults->pvBAFunctionsProcContext = pBAFunctions;
    pBAFunctions = NULL;

LExit:
    ReleaseObject(pBAFunctions);

    return hr;
}
