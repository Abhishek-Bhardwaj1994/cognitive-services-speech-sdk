//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//
// carbon_factory.cpp: Implementation definitions for CSpxResourceManager C++ class
//

#include "stdafx.h"
#include "resource_manager.h"
#include "module_factory.h"
#include "factory_helpers.h"


namespace Microsoft {
namespace CognitiveServices {
namespace Speech {
namespace Impl {


CSpxResourceManager::CSpxResourceManager()
{
    SPX_DBG_TRACE_FUNCTION();

    // **IMPORTANT**: Do NOT change the order in which module factories are added here!!!
    //
    //   They will be searched in order for objects to create (See ::CreateObject).
    //   Changing the order will have adverse side effects on the intended behavior.
    //
    //   FOR EXAMPLE: CSpxResourceManager intentionally searches for mock objects first.
    //                This allows "at runtime testing".

#ifdef __linux__
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libcarbon-mock.so"));

    // Note: due to new naming, removing any carbon prefix in name
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libMicrosoft.CognitiveServices.Speech.extension.pma.so"));
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libMicrosoft.CognitiveServices.Speech.extension.kws.so"));

    m_moduleFactories.push_back(CSpxModuleFactory::Get("carbon"));
#elif __MACH__
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libcarbon-mock.dylib"));

    // Note: due to new naming, removing any carbon prefix in name
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libMicrosoft.CognitiveServices.Speech.extension.pma.dylib"));
    m_moduleFactories.push_back(CSpxModuleFactory::Get("libMicrosoft.CognitiveServices.Speech.extension.kws.dylib"));

    m_moduleFactories.push_back(CSpxModuleFactory::Get("carbon"));
#else
    m_moduleFactories.push_back(CSpxModuleFactory::Get("carbon-mock.dll"));

    // Note: due to new naming, removing any carbon prefix in name
    // Note: due to dots in filenames, MUST append .dll suffix!
    //       (added them for consistency to all names, but the
    //       special "carbon" core component)
    m_moduleFactories.push_back(CSpxModuleFactory::Get("Microsoft.CognitiveServices.Speech.extension.pma.dll"));
    m_moduleFactories.push_back(CSpxModuleFactory::Get("Microsoft.CognitiveServices.Speech.extension.kws.dll"));

    m_moduleFactories.push_back(CSpxModuleFactory::Get("carbon")); // this is special, internal name, no dll extension!
    m_moduleFactories.push_back(CSpxModuleFactory::Get("carbon-unidec.dll"));
#endif
}

CSpxResourceManager::~CSpxResourceManager()
{
    SPX_DBG_TRACE_FUNCTION();
}

void* CSpxResourceManager::CreateObject(const char* className, const char* interfaceName)
{
    // Loop thru each of our module factories, and see if they can create the object.
    //
    // If more than one module factory can create the object, we'll use the instance
    // from the first module factory that can create it. This enables "mocking" and
    // general "replacement" following the order in which the module factories are
    // added into the module factory list (see ctor...)

    for (auto factory: m_moduleFactories)
    {
        auto obj = factory->CreateObject(className, interfaceName);
        if (obj != nullptr)
        {
            return obj;
        }
    }

    return nullptr;
}


} } } } // Microsoft::CognitiveServices::Speech::Impl