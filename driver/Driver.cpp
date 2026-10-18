// Broken Screen Resizer sanal ekran sürücüsü (IddCx / UMDF 2).
//
// Ne yapar:
//  - Windows'a "Broken Screen Resizer Virtual Display" adında bir ekran kartı tanıtır (başta monitörsüz).
//  - Broken Screen Resizer uygulaması aygıt arayüzü üzerinden IOCTL gönderip istediği çözünürlükte
//    sanal monitör ekler / kaldırır (bkz. Public.h).
//  - Her sanal monitör tam olarak istenen çözünürlüğü sunar; böylece Windows onu
//    "sağlam alan" boyutunda gerçek bir monitör gibi kullanır.
//
// Yapı, Microsoft'un IddSampleDriver örneğine dayanır (MIT lisansı).

#include "Driver.h"

using namespace std;
using namespace Microsoft::WRL;
using namespace BsrVdd;

extern "C" DRIVER_INITIALIZE DriverEntry;

EVT_WDF_DRIVER_DEVICE_ADD BsrVddDeviceAdd;
EVT_WDF_DEVICE_D0_ENTRY BsrVddDeviceD0Entry;

EVT_IDD_CX_DEVICE_IO_CONTROL BsrVddDeviceIoControl;
EVT_IDD_CX_ADAPTER_INIT_FINISHED BsrVddAdapterInitFinished;
EVT_IDD_CX_ADAPTER_COMMIT_MODES BsrVddAdapterCommitModes;

EVT_IDD_CX_PARSE_MONITOR_DESCRIPTION BsrVddParseMonitorDescription;
EVT_IDD_CX_MONITOR_GET_DEFAULT_DESCRIPTION_MODES BsrVddMonitorGetDefaultModes;
EVT_IDD_CX_MONITOR_QUERY_TARGET_MODES BsrVddMonitorQueryModes;

EVT_IDD_CX_MONITOR_ASSIGN_SWAPCHAIN BsrVddMonitorAssignSwapChain;
EVT_IDD_CX_MONITOR_UNASSIGN_SWAPCHAIN BsrVddMonitorUnassignSwapChain;

static void Log(const wchar_t* fmt, ...)
{
    wchar_t buf[512];
    va_list args;
    va_start(args, fmt);
    _vsnwprintf_s(buf, _TRUNCATE, fmt, args);
    va_end(args);
    OutputDebugStringW(L"[BrokenScreenResizerVDD] ");
    OutputDebugStringW(buf);
    OutputDebugStringW(L"\n");
}

// ===================================================================================
// EDID: Windows her monitörü EDID ile tanır. Çözünürlüğü ve yenileme hızını EDID'in seri
// numarası / ürün kodu alanlarına gömüyoruz; böylece ParseMonitorDescription'da (hangi
// monitör olduğu bilgisi gelmez) modları doğrudan EDID'den geri okuyabiliyoruz.
// Ayrıca farklı çözünürlük = farklı seri no, yani Windows eski bir çözünürlüğü hatırlayıp
// yanlış mod seçmez.
// ===================================================================================
static const BYTE EdidHeader[8] = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
static const BYTE EdidVendor[2] = { 0x0A, 0x72 }; // "BSR"

static void BuildEdid(BYTE edid[128], UINT width, UINT height, UINT refresh, UINT id)
{
    ZeroMemory(edid, 128);
    memcpy(edid, EdidHeader, 8);
    edid[8] = EdidVendor[0];
    edid[9] = EdidVendor[1];

    UINT product = ((id & 0xFF) << 8) | (refresh & 0xFF);
    edid[10] = (BYTE)(product & 0xFF);
    edid[11] = (BYTE)(product >> 8);

    UINT serial = ((width & 0xFFFF) << 16) | (height & 0xFFFF);
    edid[12] = (BYTE)(serial);
    edid[13] = (BYTE)(serial >> 8);
    edid[14] = (BYTE)(serial >> 16);
    edid[15] = (BYTE)(serial >> 24);

    edid[16] = 1;    // üretim haftası
    edid[17] = 36;   // üretim yılı - 1990 = 2026
    edid[18] = 1;    // EDID 1.4
    edid[19] = 4;
    edid[20] = 0xA5; // dijital giriş, 8 bit renk, DisplayPort

    // Fiziksel boyut (cm): 96 DPI varsayımıyla; Windows önerilen ölçeği buna göre seçer
    UINT wcm = min(255u, (width * 254u + 4800u) / 9600u);
    UINT hcm = min(255u, (height * 254u + 4800u) / 9600u);
    edid[21] = (BYTE)wcm;
    edid[22] = (BYTE)hcm;
    edid[23] = 120;  // gamma 2.2
    edid[24] = 0x06; // sRGB, tercih edilen zamanlama ilk tanımlayıcıda

    // sRGB renk koordinatları
    static const BYTE chroma[10] = { 0xEE, 0x91, 0xA3, 0x54, 0x4C, 0x99, 0x26, 0x0F, 0x50, 0x54 };
    memcpy(edid + 25, chroma, 10);

    // Standart zamanlamalar kullanılmıyor
    for (int i = 38; i < 54; i++) edid[i] = 0x01;

    // Tanımlayıcı 1: tercih edilen zamanlama (kozmetik; asıl modları sürücü bildiriyor)
    BYTE* d = edid + 54;
    UINT ha = min(width, 4095u), va = min(height, 4095u);
    UINT hb = 160, vb = max(23u, va / 20u);
    UINT hz = refresh;
    ULONGLONG clock = (ULONGLONG)(ha + hb) * (va + vb) * hz / 10000ull; // 10 kHz birimi
    while (clock > 65535 && hz > 24) { hz--; clock = (ULONGLONG)(ha + hb) * (va + vb) * hz / 10000ull; }
    if (clock > 65535) clock = 65535;
    UINT hso = 48, hsp = 32, vso = 3, vsp = 5;
    UINT hmm = min(4095u, wcm * 10), vmm = min(4095u, hcm * 10);
    d[0] = (BYTE)(clock & 0xFF);
    d[1] = (BYTE)(clock >> 8);
    d[2] = (BYTE)(ha & 0xFF);
    d[3] = (BYTE)(hb & 0xFF);
    d[4] = (BYTE)(((ha >> 8) << 4) | (hb >> 8));
    d[5] = (BYTE)(va & 0xFF);
    d[6] = (BYTE)(vb & 0xFF);
    d[7] = (BYTE)(((va >> 8) << 4) | (vb >> 8));
    d[8] = (BYTE)hso;
    d[9] = (BYTE)hsp;
    d[10] = (BYTE)((vso << 4) | vsp);
    d[11] = 0;
    d[12] = (BYTE)(hmm & 0xFF);
    d[13] = (BYTE)(vmm & 0xFF);
    d[14] = (BYTE)(((hmm >> 8) << 4) | (vmm >> 8));
    d[17] = 0x1E; // dijital ayrık senkron, +H +V

    // Tanımlayıcı 2: monitör adı
    d = edid + 72;
    d[3] = 0xFC;
    const char name[13] = { 'B','S','R',' ','V','i','r','t','u','a','l','\n',' ' };
    memcpy(d + 5, name, 13);

    // Tanımlayıcı 3 ve 4: boş (dummy)
    edid[90 + 3] = 0x10;
    edid[108 + 3] = 0x10;

    edid[126] = 0; // ek blok yok
    BYTE sum = 0;
    for (int i = 0; i < 127; i++) sum += edid[i];
    edid[127] = (BYTE)(0x100 - sum);
}

static bool ValidSize(UINT w, UINT h, UINT hz)
{
    return w >= BSRVDD_MIN_W && w <= BSRVDD_MAX_W && h >= BSRVDD_MIN_H && h <= BSRVDD_MAX_H &&
           hz >= BSRVDD_MIN_HZ && hz <= BSRVDD_MAX_HZ;
}

static bool DecodeEdid(const void* data, UINT size, UINT* w, UINT* h, UINT* hz)
{
    if (data == nullptr || size < 128) return false;
    const BYTE* e = (const BYTE*)data;
    if (memcmp(e, EdidHeader, 8) != 0 || e[8] != EdidVendor[0] || e[9] != EdidVendor[1]) return false;
    UINT serial = e[12] | (e[13] << 8) | (e[14] << 16) | ((UINT)e[15] << 24);
    *w = serial >> 16;
    *h = serial & 0xFFFF;
    *hz = e[10];
    return ValidSize(*w, *h, *hz);
}

// ===================================================================================
// Mod yardımcıları (IddSampleDriver'dan)
// ===================================================================================
static inline void FillSignalInfo(DISPLAYCONFIG_VIDEO_SIGNAL_INFO& mode, DWORD width, DWORD height, DWORD vsync, bool monitorMode)
{
    mode.totalSize.cx = mode.activeSize.cx = width;
    mode.totalSize.cy = mode.activeSize.cy = height;

    // Monitör modlarında bölen 0, hedef modlarda 1 olmalı
    mode.AdditionalSignalInfo.vSyncFreqDivider = monitorMode ? 0 : 1;
    mode.AdditionalSignalInfo.videoStandard = 255;

    mode.vSyncFreq.Numerator = vsync;
    mode.vSyncFreq.Denominator = 1;
    mode.hSyncFreq.Numerator = vsync * height;
    mode.hSyncFreq.Denominator = 1;

    mode.scanLineOrdering = DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE;
    mode.pixelRate = ((UINT64)vsync) * ((UINT64)width) * ((UINT64)height);
}

static IDDCX_MONITOR_MODE CreateMonitorMode(DWORD width, DWORD height, DWORD vsync, IDDCX_MONITOR_MODE_ORIGIN origin)
{
    IDDCX_MONITOR_MODE mode = {};
    mode.Size = sizeof(mode);
    mode.Origin = origin;
    FillSignalInfo(mode.MonitorVideoSignalInfo, width, height, vsync, true);
    return mode;
}

static IDDCX_TARGET_MODE CreateTargetMode(DWORD width, DWORD height, DWORD vsync)
{
    IDDCX_TARGET_MODE mode = {};
    mode.Size = sizeof(mode);
    FillSignalInfo(mode.TargetVideoSignalInfo.targetVideoSignalInfo, width, height, vsync, false);
    return mode;
}

// ===================================================================================
// Sürücü giriş noktası ve WDF/IddCx geri çağrıları
// ===================================================================================
extern "C" BOOL WINAPI DllMain(_In_ HINSTANCE, _In_ UINT, _In_opt_ LPVOID)
{
    return TRUE;
}

_Use_decl_annotations_
extern "C" NTSTATUS DriverEntry(PDRIVER_OBJECT pDriverObject, PUNICODE_STRING pRegistryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    WDF_DRIVER_CONFIG_INIT(&config, BsrVddDeviceAdd);
    return WdfDriverCreate(pDriverObject, pRegistryPath, &attributes, &config, WDF_NO_HANDLE);
}

_Use_decl_annotations_
NTSTATUS BsrVddDeviceAdd(WDFDRIVER driver, PWDFDEVICE_INIT pDeviceInit)
{
    UNREFERENCED_PARAMETER(driver);

    WDF_PNPPOWER_EVENT_CALLBACKS pnpPower;
    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnpPower);
    pnpPower.EvtDeviceD0Entry = BsrVddDeviceD0Entry;
    WdfDeviceInitSetPnpPowerEventCallbacks(pDeviceInit, &pnpPower);

    IDD_CX_CLIENT_CONFIG iddConfig;
    IDD_CX_CLIENT_CONFIG_INIT(&iddConfig);
    iddConfig.EvtIddCxDeviceIoControl = BsrVddDeviceIoControl;
    iddConfig.EvtIddCxAdapterInitFinished = BsrVddAdapterInitFinished;
    iddConfig.EvtIddCxParseMonitorDescription = BsrVddParseMonitorDescription;
    iddConfig.EvtIddCxMonitorGetDefaultDescriptionModes = BsrVddMonitorGetDefaultModes;
    iddConfig.EvtIddCxMonitorQueryTargetModes = BsrVddMonitorQueryModes;
    iddConfig.EvtIddCxAdapterCommitModes = BsrVddAdapterCommitModes;
    iddConfig.EvtIddCxMonitorAssignSwapChain = BsrVddMonitorAssignSwapChain;
    iddConfig.EvtIddCxMonitorUnassignSwapChain = BsrVddMonitorUnassignSwapChain;

    NTSTATUS status = IddCxDeviceInitConfig(pDeviceInit, &iddConfig);
    if (!NT_SUCCESS(status)) return status;

    WDF_OBJECT_ATTRIBUTES attr;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attr, IndirectDeviceContextWrapper);
    attr.EvtCleanupCallback = [](WDFOBJECT object)
    {
        auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(object);
        if (wrapper) wrapper->Cleanup();
    };

    WDFDEVICE device = nullptr;
    status = WdfDeviceCreate(&pDeviceInit, &attr, &device);
    if (!NT_SUCCESS(status)) return status;

    // Uygulamanın bizi bulması için aygıt arayüzü
    status = WdfDeviceCreateDeviceInterface(device, &GUID_DEVINTERFACE_BSR_VDD, nullptr);
    if (!NT_SUCCESS(status)) return status;

    status = IddCxDeviceInitialize(device);
    if (!NT_SUCCESS(status)) return status;

    auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(device);
    wrapper->pContext = new (nothrow) IndirectDeviceContext(device);
    if (wrapper->pContext == nullptr) return STATUS_INSUFFICIENT_RESOURCES;
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddDeviceD0Entry(WDFDEVICE device, WDF_POWER_DEVICE_STATE previousState)
{
    UNREFERENCED_PARAMETER(previousState);
    auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(device);
    if (wrapper && wrapper->pContext) wrapper->pContext->InitAdapter();
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
VOID BsrVddDeviceIoControl(WDFDEVICE device, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code)
{
    auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(device);
    if (wrapper == nullptr || wrapper->pContext == nullptr)
    {
        WdfRequestComplete(request, STATUS_DEVICE_NOT_READY);
        return;
    }
    wrapper->pContext->HandleIoctl(request, outputLength, inputLength, code);
}

_Use_decl_annotations_
NTSTATUS BsrVddAdapterInitFinished(IDDCX_ADAPTER adapterObject, const IDARG_IN_ADAPTER_INIT_FINISHED* pInArgs)
{
    auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(adapterObject);
    if (wrapper && wrapper->pContext) wrapper->pContext->OnAdapterInitFinished(pInArgs->AdapterInitStatus);
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddAdapterCommitModes(IDDCX_ADAPTER adapterObject, const IDARG_IN_COMMITMODES* pInArgs)
{
    UNREFERENCED_PARAMETER(adapterObject);
    UNREFERENCED_PARAMETER(pInArgs);
    // Gerçek bir donanım yok; mod değişiminde yapılacak bir şey yok.
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddParseMonitorDescription(const IDARG_IN_PARSEMONITORDESCRIPTION* pInArgs, IDARG_OUT_PARSEMONITORDESCRIPTION* pOutArgs)
{
    UINT w, h, hz;
    if (!DecodeEdid(pInArgs->MonitorDescription.pData, pInArgs->MonitorDescription.DataSize, &w, &h, &hz))
        return STATUS_INVALID_PARAMETER;

    pOutArgs->MonitorModeBufferOutputCount = 1;
    if (pInArgs->MonitorModeBufferInputCount < 1)
        return pInArgs->MonitorModeBufferInputCount > 0 ? STATUS_BUFFER_TOO_SMALL : STATUS_SUCCESS;

    pInArgs->pMonitorModes[0] = CreateMonitorMode(w, h, hz, IDDCX_MONITOR_MODE_ORIGIN_MONITORDESCRIPTOR);
    pOutArgs->PreferredMonitorModeIdx = 0;
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddMonitorGetDefaultModes(IDDCX_MONITOR monitorObject, const IDARG_IN_GETDEFAULTDESCRIPTIONMODES* pInArgs, IDARG_OUT_GETDEFAULTDESCRIPTIONMODES* pOutArgs)
{
    // Her monitöre EDID verdiğimiz için normalde çağrılmaz; yine de monitör bilgisinden cevap veriyoruz.
    auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(monitorObject);
    if (wrapper == nullptr || wrapper->pContext == nullptr) return STATUS_INVALID_PARAMETER;
    auto* m = wrapper->pContext;

    pOutArgs->DefaultMonitorModeBufferOutputCount = 1;
    if (pInArgs->DefaultMonitorModeBufferInputCount < 1)
        return pInArgs->DefaultMonitorModeBufferInputCount > 0 ? STATUS_BUFFER_TOO_SMALL : STATUS_SUCCESS;

    pInArgs->pDefaultMonitorModes[0] = CreateMonitorMode(m->Width, m->Height, m->Refresh, IDDCX_MONITOR_MODE_ORIGIN_DRIVER);
    pOutArgs->PreferredMonitorModeIdx = 0;
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddMonitorQueryModes(IDDCX_MONITOR monitorObject, const IDARG_IN_QUERYTARGETMODES* pInArgs, IDARG_OUT_QUERYTARGETMODES* pOutArgs)
{
    auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(monitorObject);
    if (wrapper == nullptr || wrapper->pContext == nullptr) return STATUS_INVALID_PARAMETER;
    auto* m = wrapper->pContext;

    pOutArgs->TargetModeBufferOutputCount = 1;
    if (pInArgs->TargetModeBufferInputCount >= 1)
        pInArgs->pTargetModes[0] = CreateTargetMode(m->Width, m->Height, m->Refresh);
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddMonitorAssignSwapChain(IDDCX_MONITOR monitorObject, const IDARG_IN_SETSWAPCHAIN* pInArgs)
{
    auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(monitorObject);
    if (wrapper == nullptr || wrapper->pContext == nullptr) return STATUS_INVALID_PARAMETER;
    wrapper->pContext->AssignSwapChain(pInArgs->hSwapChain, pInArgs->RenderAdapterLuid, pInArgs->hNextSurfaceAvailable);
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
NTSTATUS BsrVddMonitorUnassignSwapChain(IDDCX_MONITOR monitorObject)
{
    auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(monitorObject);
    if (wrapper && wrapper->pContext) wrapper->pContext->UnassignSwapChain();
    return STATUS_SUCCESS;
}

// ===================================================================================
// Direct3DDevice
// ===================================================================================
Direct3DDevice::Direct3DDevice(LUID adapterLuid) : AdapterLuid(adapterLuid)
{
}

HRESULT Direct3DDevice::Init()
{
    HRESULT hr = CreateDXGIFactory2(0, IID_PPV_ARGS(&DxgiFactory));
    if (FAILED(hr)) return hr;

    hr = DxgiFactory->EnumAdapterByLuid(AdapterLuid, IID_PPV_ARGS(&Adapter));
    if (FAILED(hr)) return hr;

    return D3D11CreateDevice(Adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                             nullptr, 0, D3D11_SDK_VERSION, &Device, nullptr, &DeviceContext);
}

// ===================================================================================
// SwapChainProcessor
// ===================================================================================
SwapChainProcessor::SwapChainProcessor(IDDCX_SWAPCHAIN swapChain, shared_ptr<Direct3DDevice> device, HANDLE newFrameEvent)
    : m_hSwapChain(swapChain), m_Device(device), m_hAvailableBufferEvent(newFrameEvent)
{
    m_hTerminateEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    m_hThread = CreateThread(nullptr, 0, RunThread, this, 0, nullptr);
}

SwapChainProcessor::~SwapChainProcessor()
{
    if (m_hTerminateEvent) SetEvent(m_hTerminateEvent);
    if (m_hThread)
    {
        WaitForSingleObject(m_hThread, INFINITE);
        CloseHandle(m_hThread);
    }
    if (m_hTerminateEvent) CloseHandle(m_hTerminateEvent);
}

DWORD CALLBACK SwapChainProcessor::RunThread(LPVOID argument)
{
    reinterpret_cast<SwapChainProcessor*>(argument)->Run();
    return 0;
}

void SwapChainProcessor::Run()
{
    DWORD avTask = 0;
    HANDLE avTaskHandle = AvSetMmThreadCharacteristicsW(L"Distribution", &avTask);

    RunCore();

    // Swap chain'i bırak; Windows gerekirse yenisini verir
    WdfObjectDelete((WDFOBJECT)m_hSwapChain);
    m_hSwapChain = nullptr;

    if (avTaskHandle) AvRevertMmThreadCharacteristics(avTaskHandle);
}

void SwapChainProcessor::RunCore()
{
    ComPtr<IDXGIDevice> dxgiDevice;
    HRESULT hr = m_Device->Device.As(&dxgiDevice);
    if (FAILED(hr)) return;

    IDARG_IN_SWAPCHAINSETDEVICE setDevice = {};
    setDevice.pDevice = dxgiDevice.Get();
    hr = IddCxSwapChainSetDevice(m_hSwapChain, &setDevice);
    if (FAILED(hr)) return;

    for (;;)
    {
        ComPtr<IDXGIResource> acquiredBuffer;
        IDARG_OUT_RELEASEANDACQUIREBUFFER buffer = {};
        hr = IddCxSwapChainReleaseAndAcquireBuffer(m_hSwapChain, &buffer);

        if (hr == E_PENDING)
        {
            HANDLE waitHandles[] = { m_hAvailableBufferEvent, m_hTerminateEvent };
            DWORD waitResult = WaitForMultipleObjects(ARRAYSIZE(waitHandles), waitHandles, FALSE, 16);
            if (waitResult == WAIT_OBJECT_0 || waitResult == WAIT_TIMEOUT) continue;
            break; // sonlandır ya da hata
        }
        else if (SUCCEEDED(hr))
        {
            // Kare elimizde; içeriğe ihtiyacımız yok, referansı bırakıp Windows'a geri veriyoruz
            acquiredBuffer.Attach(buffer.MetaData.pSurface);
            acquiredBuffer.Reset();

            hr = IddCxSwapChainFinishedProcessingFrame(m_hSwapChain);
            if (FAILED(hr)) break;
        }
        else
        {
            // Swap chain kayboldu (ör. mod değişti); Windows yenisini atayacak
            break;
        }
    }
}

// ===================================================================================
// IndirectMonitorContext
// ===================================================================================
IndirectMonitorContext::IndirectMonitorContext(IDDCX_MONITOR monitor, UINT width, UINT height, UINT refresh)
    : Width(width), Height(height), Refresh(refresh), m_Monitor(monitor)
{
}

IndirectMonitorContext::~IndirectMonitorContext()
{
    m_ProcessingThread.reset();
}

void IndirectMonitorContext::AssignSwapChain(IDDCX_SWAPCHAIN swapChain, LUID renderAdapter, HANDLE newFrameEvent)
{
    m_ProcessingThread.reset();

    auto device = make_shared<Direct3DDevice>(renderAdapter);
    if (FAILED(device->Init()))
    {
        // D3D cihazı açılamadı; swap chain'i silince Windows tekrar dener
        WdfObjectDelete(swapChain);
        return;
    }
    m_ProcessingThread.reset(new SwapChainProcessor(swapChain, device, newFrameEvent));
}

void IndirectMonitorContext::UnassignSwapChain()
{
    m_ProcessingThread.reset();
}

// ===================================================================================
// IndirectDeviceContext: adaptör + monitör yönetimi
// ===================================================================================
IndirectDeviceContext::IndirectDeviceContext(WDFDEVICE wdfDevice) : m_WdfDevice(wdfDevice)
{
    m_Worker = thread(&IndirectDeviceContext::WorkerLoop, this);
}

IndirectDeviceContext::~IndirectDeviceContext()
{
    {
        lock_guard<mutex> lock(m_Lock);
        m_Stop = true;
        m_Queue.clear();
    }
    m_Cv.notify_all();
    if (m_Worker.joinable()) m_Worker.join();
}

void IndirectDeviceContext::InitAdapter()
{
    // D0Entry uyku dönüşünde de çağrılır; adaptör yalnızca bir kez oluşturulur
    if (m_Adapter != nullptr) return;

    IDDCX_ADAPTER_CAPS caps = {};
    caps.Size = sizeof(caps);
    caps.MaxMonitorsSupported = BSRVDD_MAX_MONITORS;

    caps.EndPointDiagnostics.Size = sizeof(caps.EndPointDiagnostics);
    caps.EndPointDiagnostics.GammaSupport = IDDCX_FEATURE_IMPLEMENTATION_NONE;
    caps.EndPointDiagnostics.TransmissionType = IDDCX_TRANSMISSION_TYPE_WIRED_OTHER;
    caps.EndPointDiagnostics.pEndPointFriendlyName = L"Broken Screen Resizer Virtual Display";
    caps.EndPointDiagnostics.pEndPointManufacturerName = L"Broken Screen Resizer";
    caps.EndPointDiagnostics.pEndPointModelName = L"BrokenScreenResizerVDD";

    IDDCX_ENDPOINT_VERSION version = {};
    version.Size = sizeof(version);
    version.MajorVer = 1;
    version.MinorVer = 3;
    caps.EndPointDiagnostics.pFirmwareVersion = &version;
    caps.EndPointDiagnostics.pHardwareVersion = &version;

    WDF_OBJECT_ATTRIBUTES attr;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attr, IndirectDeviceContextWrapper);

    IDARG_IN_ADAPTER_INIT adapterInit = {};
    adapterInit.WdfDevice = m_WdfDevice;
    adapterInit.pCaps = &caps;
    adapterInit.ObjectAttributes = &attr;

    IDARG_OUT_ADAPTER_INIT adapterInitOut = {};
    NTSTATUS status = IddCxAdapterInitAsync(&adapterInit, &adapterInitOut);
    if (NT_SUCCESS(status))
    {
        m_Adapter = adapterInitOut.AdapterObject;
        // Adaptör nesnesinin bağlamı aygıt bağlamını gösterir (silme işi aygıtın temizliğinde)
        auto* wrapper = WdfObjectGet_IndirectDeviceContextWrapper(adapterInitOut.AdapterObject);
        wrapper->pContext = this;
    }
    else
    {
        Log(L"IddCxAdapterInitAsync hata: 0x%08X", status);
    }
}

void IndirectDeviceContext::OnAdapterInitFinished(NTSTATUS status)
{
    lock_guard<mutex> lock(m_Lock);
    m_AdapterReady = NT_SUCCESS(status);
    Log(L"Adaptör hazır: 0x%08X", status);
}

NTSTATUS IndirectDeviceContext::QueueAdd(UINT width, UINT height, UINT refresh, UINT* id)
{
    if (!ValidSize(width, height, refresh)) return STATUS_INVALID_PARAMETER;

    lock_guard<mutex> lock(m_Lock);
    if (!m_AdapterReady) return STATUS_DEVICE_NOT_READY;

    // Aynı boyutta bir monitör zaten varsa onu döndür
    for (UINT i = 0; i < BSRVDD_MAX_MONITORS; i++)
    {
        auto& s = m_Slots[i];
        if (s.Used && (s.State == BSRVDD_STATE_ACTIVE || s.State == BSRVDD_STATE_PENDING) &&
            s.Width == width && s.Height == height && s.Refresh == refresh)
        {
            *id = i + 1;
            return STATUS_SUCCESS;
        }
    }

    for (UINT i = 0; i < BSRVDD_MAX_MONITORS; i++)
    {
        auto& s = m_Slots[i];
        // Başarısız kalmış yuvalar yeniden kullanılabilir
        if (s.Used && !(s.State == BSRVDD_STATE_FAILED && s.Monitor == nullptr)) continue;
        s = MonitorSlot();
        s.Used = true;
        s.Width = width;
        s.Height = height;
        s.Refresh = refresh;
        s.State = BSRVDD_STATE_PENDING;
        m_Queue.push_back({ true, i });
        m_Cv.notify_all();
        *id = i + 1;
        return STATUS_SUCCESS;
    }
    return STATUS_INSUFFICIENT_RESOURCES;
}

void IndirectDeviceContext::QueueRemove(UINT id)
{
    lock_guard<mutex> lock(m_Lock);
    for (UINT i = 0; i < BSRVDD_MAX_MONITORS; i++)
    {
        if (id != 0 && id != i + 1) continue;
        auto& s = m_Slots[i];
        if (!s.Used || s.State == BSRVDD_STATE_REMOVING) continue;
        s.State = BSRVDD_STATE_REMOVING;
        m_Queue.push_back({ false, i });
    }
    m_Cv.notify_all();
}

// IddCx monitör ekleme/çıkarma çağrıları IOCTL geri çağrısının içinden yapılmaz;
// ayrı bir iş parçacığında sırayla yürütülür (kilitlenme riski olmasın diye).
void IndirectDeviceContext::WorkerLoop()
{
    for (;;)
    {
        Command cmd;
        {
            unique_lock<mutex> lock(m_Lock);
            m_Cv.wait(lock, [this] { return m_Stop || !m_Queue.empty(); });
            if (m_Stop) return;
            cmd = m_Queue.front();
            m_Queue.pop_front();
        }
        if (cmd.Add) DoAdd(cmd.Slot);
        else DoRemove(cmd.Slot);
    }
}

void IndirectDeviceContext::DoAdd(UINT slot)
{
    UINT w, h, hz;
    {
        lock_guard<mutex> lock(m_Lock);
        auto& s = m_Slots[slot];
        if (!s.Used || s.State != BSRVDD_STATE_PENDING) return; // bu arada kaldırılması istenmiş
        w = s.Width; h = s.Height; hz = s.Refresh;
    }

    BYTE edid[128];
    BuildEdid(edid, w, h, hz, slot + 1);

    WDF_OBJECT_ATTRIBUTES attr;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attr, IndirectMonitorContextWrapper);
    attr.EvtCleanupCallback = [](WDFOBJECT object)
    {
        auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(object);
        if (wrapper) wrapper->Cleanup();
    };

    IDDCX_MONITOR_INFO info = {};
    info.Size = sizeof(info);
    info.MonitorType = DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HDMI;
    info.ConnectorIndex = slot;
    info.MonitorDescription.Size = sizeof(info.MonitorDescription);
    info.MonitorDescription.Type = IDDCX_MONITOR_DESCRIPTION_TYPE_EDID;
    info.MonitorDescription.DataSize = sizeof(edid);
    info.MonitorDescription.pData = edid;
    // Her yuva için sabit bir kimlik: {DB429C52-D650-4643-8385-50F8FB328120 + yuva}
    info.MonitorContainerId = { 0xdb429c52, 0xd650, 0x4643, { 0x83, 0x85, 0x50, 0xf8, 0xfb, 0x32, 0x81, (BYTE)(0x20 + slot) } };

    IDARG_IN_MONITORCREATE create = {};
    create.ObjectAttributes = &attr;
    create.pMonitorInfo = &info;

    IDARG_OUT_MONITORCREATE createOut = {};
    NTSTATUS status = IddCxMonitorCreate(m_Adapter, &create, &createOut);
    IDDCX_MONITOR monitor = nullptr;
    if (NT_SUCCESS(status))
    {
        monitor = createOut.MonitorObject;
        auto* wrapper = WdfObjectGet_IndirectMonitorContextWrapper(monitor);
        wrapper->pContext = new (nothrow) IndirectMonitorContext(monitor, w, h, hz);
        if (wrapper->pContext == nullptr)
        {
            status = STATUS_INSUFFICIENT_RESOURCES;
            WdfObjectDelete(monitor);
            monitor = nullptr;
        }
    }
    if (NT_SUCCESS(status))
    {
        IDARG_OUT_MONITORARRIVAL arrivalOut = {};
        status = IddCxMonitorArrival(monitor, &arrivalOut);
        if (!NT_SUCCESS(status))
        {
            WdfObjectDelete(monitor);
            monitor = nullptr;
        }
    }
    Log(L"Monitör %u ekleme %ux%u@%u: 0x%08X", slot + 1, w, h, hz, status);

    lock_guard<mutex> lock(m_Lock);
    auto& s = m_Slots[slot];
    s.Status = status;
    s.Monitor = monitor;
    if (s.State == BSRVDD_STATE_PENDING) s.State = NT_SUCCESS(status) ? BSRVDD_STATE_ACTIVE : BSRVDD_STATE_FAILED;
}

void IndirectDeviceContext::DoRemove(UINT slot)
{
    IDDCX_MONITOR monitor;
    {
        lock_guard<mutex> lock(m_Lock);
        monitor = m_Slots[slot].Monitor;
    }
    if (monitor != nullptr)
    {
        // Departure sonrası monitör nesnesini IddCx temizler (bağlamı Cleanup siler)
        NTSTATUS status = IddCxMonitorDeparture(monitor);
        Log(L"Monitör %u kaldırıldı: 0x%08X", slot + 1, status);
    }
    lock_guard<mutex> lock(m_Lock);
    m_Slots[slot] = MonitorSlot();
}

void IndirectDeviceContext::HandleIoctl(WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code)
{
    UNREFERENCED_PARAMETER(inputLength);
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    size_t info = 0;

    switch (code)
    {
    case IOCTL_BSRVDD_INFO:
    {
        BSRVDD_INFO* out = nullptr;
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(BSRVDD_INFO), (PVOID*)&out, nullptr);
        if (!NT_SUCCESS(status)) break;
        lock_guard<mutex> lock(m_Lock);
        out->Version = BSRVDD_PROTOCOL_VERSION;
        out->MaxMonitors = BSRVDD_MAX_MONITORS;
        out->AdapterReady = m_AdapterReady ? 1 : 0;
        out->MonitorCount = 0;
        for (auto& s : m_Slots) if (s.Used) out->MonitorCount++;
        info = sizeof(BSRVDD_INFO);
        break;
    }
    case IOCTL_BSRVDD_ADD:
    {
        BSRVDD_ADD_IN* in = nullptr;
        BSRVDD_ADD_OUT* out = nullptr;
        status = WdfRequestRetrieveInputBuffer(request, sizeof(BSRVDD_ADD_IN), (PVOID*)&in, nullptr);
        if (!NT_SUCCESS(status)) break;
        BSRVDD_ADD_IN args = *in; // METHOD_BUFFERED: giriş ve çıkış aynı tamponu paylaşır
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(BSRVDD_ADD_OUT), (PVOID*)&out, nullptr);
        if (!NT_SUCCESS(status)) break;
        UINT id = 0;
        status = QueueAdd(args.Width, args.Height, args.RefreshRate, &id);
        if (NT_SUCCESS(status))
        {
            out->Id = id;
            info = sizeof(BSRVDD_ADD_OUT);
        }
        break;
    }
    case IOCTL_BSRVDD_REMOVE:
    {
        BSRVDD_REMOVE_IN* in = nullptr;
        status = WdfRequestRetrieveInputBuffer(request, sizeof(BSRVDD_REMOVE_IN), (PVOID*)&in, nullptr);
        if (!NT_SUCCESS(status)) break;
        QueueRemove(in->Id);
        status = STATUS_SUCCESS;
        break;
    }
    case IOCTL_BSRVDD_LIST:
    {
        BSRVDD_MONITOR_STATE* out = nullptr;
        size_t len = 0;
        if (outputLength < sizeof(BSRVDD_MONITOR_STATE))
        {
            // Tampon yok ya da küçük: hiç monitör yoksa boş liste geçerli bir cevap
            status = STATUS_SUCCESS;
            lock_guard<mutex> lock(m_Lock);
            for (auto& s : m_Slots) if (s.Used) { status = STATUS_BUFFER_TOO_SMALL; break; }
            break;
        }
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(BSRVDD_MONITOR_STATE), (PVOID*)&out, &len);
        if (!NT_SUCCESS(status)) break;
        size_t max = len / sizeof(BSRVDD_MONITOR_STATE), n = 0;
        lock_guard<mutex> lock(m_Lock);
        for (UINT i = 0; i < BSRVDD_MAX_MONITORS && n < max; i++)
        {
            auto& s = m_Slots[i];
            if (!s.Used) continue;
            out[n].Id = i + 1;
            out[n].Width = s.Width;
            out[n].Height = s.Height;
            out[n].RefreshRate = s.Refresh;
            out[n].State = s.State;
            out[n].Status = s.Status;
            n++;
        }
        info = n * sizeof(BSRVDD_MONITOR_STATE);
        break;
    }
    }

    WdfRequestCompleteWithInformation(request, status, info);
}
