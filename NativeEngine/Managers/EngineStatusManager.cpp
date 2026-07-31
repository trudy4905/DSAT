#include "EngineStatusManager.h"
#include <windows.h>

EngineStatusManager& EngineStatusManager::GetInstance() {
    static EngineStatusManager instance;
    return instance;
}

bool EngineStatusManager::Initialize() {
    if (m_isInitialized) {
        LogMessage(1, "Engine is already initialized.");
        return true;
    }

    m_isInitialized = true;
    m_totalProcessedItems = 0;
    m_lastExecutionTimeMs = 0.0;

    LogMessage(0, "C++ Core Engine initialized successfully.");
    return true;
}

void EngineStatusManager::Shutdown() {
    if (!m_isInitialized) return;

    m_isSimulating = false;
    m_isInitialized = false;
    LogMessage(0, "C++ Core Engine shut down.");
}

void EngineStatusManager::GetStatus(EngineStatusInfo* outStatus) {
    if (!outStatus) return;

    SYSTEM_INFO sysInfo;
    GetSystemInfo(&sysInfo);

    outStatus->isRunning = m_isInitialized ? 1 : 0;
    outStatus->coreCount = static_cast<int32_t>(sysInfo.dwNumberOfProcessors);
    outStatus->lastExecutionTimeMs = m_lastExecutionTimeMs.load();
    outStatus->totalProcessedItems = m_totalProcessedItems.load();
}

void EngineStatusManager::SetProgressCallback(EngineProgressCallback callback) {
    std::lock_guard<std::recursive_mutex> lock(m_callbackMutex);
    m_progressCallback = callback;
    LogMessage(0, "Progress callback registered.");
}

void EngineStatusManager::SetLogCallback(EngineLogCallback callback) {
    std::lock_guard<std::recursive_mutex> lock(m_callbackMutex);
    m_logCallback = callback;
    LogMessage(0, "Log callback registered.");
}

void EngineStatusManager::LogMessage(int level, const char* msg) {
    std::lock_guard<std::recursive_mutex> lock(m_callbackMutex);
    if (m_logCallback) {
        m_logCallback(level, msg);
    }
}

void EngineStatusManager::NotifyProgress(int percent, const char* statusMsg) {
    std::lock_guard<std::recursive_mutex> lock(m_callbackMutex);
    if (m_progressCallback) {
        m_progressCallback(percent, statusMsg);
    }
}
