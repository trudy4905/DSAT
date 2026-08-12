#ifndef ENGINE_STATUS_MANAGER_H
#define ENGINE_STATUS_MANAGER_H

#include "../NativeEngineApi.h"
#include <atomic>
#include <mutex>

class EngineStatusManager {
private:
    std::atomic<bool> m_isInitialized{false};
    std::atomic<bool> m_isSimulating{false};
    std::atomic<uint64_t> m_totalProcessedItems{0};
    std::atomic<double> m_lastExecutionTimeMs{0.0};

    EngineProgressCallback m_progressCallback = nullptr;
    EngineLogCallback m_logCallback = nullptr;
    std::recursive_mutex m_callbackMutex;

    EngineStatusManager() = default;

public:
    static EngineStatusManager& GetInstance();

    bool Initialize();
    void Shutdown();
    void GetStatus(EngineStatusInfo* outStatus);

    void SetProgressCallback(EngineProgressCallback callback);
    void SetLogCallback(EngineLogCallback callback);

    void LogMessage(int level, const char* msg);
    void NotifyProgress(int percent, const char* statusMsg);

    bool IsInitialized() const { return m_isInitialized.load(); }
};

#endif /* ENGINE_STATUS_MANAGER_H */
