#pragma once
// AFK4.NET BAFunctions - precompiled/common header.

#include <windows.h>

#pragma warning(push)
#pragma warning(disable:4458) // declaration of 'xxx' hides class member (gdiplus)
#include <gdiplus.h>
#pragma warning(pop)

#include <msiquery.h>
#include <objbase.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <stdlib.h>
#include <strsafe.h>
#include <CommCtrl.h>

// WiX DUtil (WixToolset.DUtil package)
#include "dutil.h"
#include "memutil.h"
#include "strutil.h"
#include "pathutil.h"
#include "fileutil.h"
#include "dictutil.h"
#include "regutil.h"

// WiX BalUtil + Bootstrapper API (WixToolset.BootstrapperApplicationApi package)
#include "BootstrapperApplicationBase.h"
#include "balutil.h"

// BAFunctions SDK (vendored from WiX v7.0.0 under wixsdk/)
#include "BAFunctions.h"
#include "IBAFunctions.h"
#include "BalBaseBAFunctions.h"
#include "BalBaseBAFunctionsProc.h"

HRESULT WINAPI CreateBAFunctions(
    __in HMODULE hModule,
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    );
