// Broken Screen Resizer sanal ekran sürücüsü (IddCx / UMDF 2).
// Yapı, Microsoft'un IddSampleDriver örneğine dayanır (MIT lisansı).
#pragma once

#define NOMINMAX
#include <windows.h>
#include <bugcodes.h>
#include <wudfwdm.h>
#include <wdf.h>
#include <iddcx.h>

#include <dxgi1_5.h>
#include <d3d11_2.h>
#include <avrt.h>
#include <wrl.h>

#include <memory>
#include <mutex>
#include <thread>
#include <condition_variable>
#include <deque>

#include "Public.h"

namespace BsrVdd
{
    // Windows'un bu monitör için seçtiği ekran kartında açılan D3D cihazı
    struct Direct3DDevice
    {
        explicit Direct3DDevice(LUID adapterLuid);
        HRESULT Init();

        LUID AdapterLuid;
        Microsoft::WRL::ComPtr<IDXGIFactory5> DxgiFactory;
        Microsoft::WRL::ComPtr<IDXGIAdapter1> Adapter;
        Microsoft::WRL::ComPtr<ID3D11Device> Device;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext> DeviceContext;
    };

    // Windows'un sanal monitöre çizdiği kareleri teslim alan iş parçacığı.
    // Kareleri bir yere göndermiyoruz; Broken Screen Resizer görüntüyü Büyüteç API'siyle masaüstünden okur.
    // Yine de karelerin alınıp geri verilmesi gerekir, yoksa Windows sanal monitörü günceller durmaz.
    class SwapChainProcessor
    {
    public:
        SwapChainProcessor(IDDCX_SWAPCHAIN swapChain, std::shared_ptr<Direct3DDevice> device, HANDLE newFrameEvent);
        ~SwapChainProcessor();

    private:
        static DWORD CALLBACK RunThread(LPVOID argument);
        void Run();
        void RunCore();

        IDDCX_SWAPCHAIN m_hSwapChain;
        std::shared_ptr<Direct3DDevice> m_Device;
        HANDLE m_hAvailableBufferEvent;
        HANDLE m_hThread = nullptr;
        HANDLE m_hTerminateEvent = nullptr;
    };

    class IndirectMonitorContext
    {
    public:
        IndirectMonitorContext(IDDCX_MONITOR monitor, UINT width, UINT height, UINT refresh);
        ~IndirectMonitorContext();

        void AssignSwapChain(IDDCX_SWAPCHAIN swapChain, LUID renderAdapter, HANDLE newFrameEvent);
        void UnassignSwapChain();

        const UINT Width, Height, Refresh;

    private:
        IDDCX_MONITOR m_Monitor;
        std::unique_ptr<SwapChainProcessor> m_ProcessingThread;
    };

    struct MonitorSlot
    {
        bool Used = false;
        UINT Width = 0, Height = 0, Refresh = 0;
        UINT State = BSRVDD_STATE_PENDING;
        NTSTATUS Status = 0;
        IDDCX_MONITOR Monitor = nullptr;
    };

    class IndirectDeviceContext
    {
    public:
        explicit IndirectDeviceContext(WDFDEVICE wdfDevice);
        ~IndirectDeviceContext();

        void InitAdapter();
        void OnAdapterInitFinished(NTSTATUS status);
        void HandleIoctl(WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code);

    private:
        struct Command { bool Add; UINT Slot; };

        NTSTATUS QueueAdd(UINT width, UINT height, UINT refresh, UINT* id);
        void QueueRemove(UINT id);
        void WorkerLoop();
        void DoAdd(UINT slot);
        void DoRemove(UINT slot);

        WDFDEVICE m_WdfDevice;
        IDDCX_ADAPTER m_Adapter = nullptr;
        bool m_AdapterReady = false;

        std::mutex m_Lock;
        MonitorSlot m_Slots[BSRVDD_MAX_MONITORS];
        std::deque<Command> m_Queue;
        std::condition_variable m_Cv;
        bool m_Stop = false;
        std::thread m_Worker;
    };
}

struct IndirectDeviceContextWrapper
{
    BsrVdd::IndirectDeviceContext* pContext;

    void Cleanup()
    {
        delete pContext;
        pContext = nullptr;
    }
};

struct IndirectMonitorContextWrapper
{
    BsrVdd::IndirectMonitorContext* pContext;

    void Cleanup()
    {
        delete pContext;
        pContext = nullptr;
    }
};

WDF_DECLARE_CONTEXT_TYPE(IndirectDeviceContextWrapper);
WDF_DECLARE_CONTEXT_TYPE(IndirectMonitorContextWrapper);
