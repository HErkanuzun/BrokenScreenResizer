// Broken Screen Resizer sanal ekran sürücüsü - uygulama ile sürücü arasındaki ortak tanımlar.
// Aynı değerler C# tarafında src\VirtualDriver.cs içinde de var; birini değiştirirsen diğerini de değiştir.
#pragma once

#include <initguid.h>

// Uygulamanın sürücüyü bulduğu aygıt arayüzü
// {69A8BDE3-4C78-4BA6-AD55-75A6B2760CA4}
DEFINE_GUID(GUID_DEVINTERFACE_BSR_VDD,
    0x69a8bde3, 0x4c78, 0x4ba6, 0xad, 0x55, 0x75, 0xa6, 0xb2, 0x76, 0x0c, 0xa4);

// 1: v0.6-v1.01. 2 (v1.5): en fazla 8 monitör, aynı boyutta birden fazla monitör (BSRVDD_ADD_NEW)
#define BSRVDD_PROTOCOL_VERSION 2
#define BSRVDD_MAX_MONITORS     8

// CTL_CODE(FILE_DEVICE_UNKNOWN, fn, METHOD_BUFFERED, FILE_ANY_ACCESS)
#define BSRVDD_CTL(fn) (((0x22) << 16) | ((fn) << 2))
#define IOCTL_BSRVDD_INFO   BSRVDD_CTL(0x900) // çıkış: BSRVDD_INFO
#define IOCTL_BSRVDD_ADD    BSRVDD_CTL(0x901) // giriş: BSRVDD_ADD_IN, çıkış: BSRVDD_ADD_OUT
#define IOCTL_BSRVDD_REMOVE BSRVDD_CTL(0x902) // giriş: BSRVDD_REMOVE_IN (Id = 0 hepsini kaldırır)
#define IOCTL_BSRVDD_LIST   BSRVDD_CTL(0x903) // çıkış: BSRVDD_MONITOR_STATE dizisi

#pragma pack(push, 4)

typedef struct BSRVDD_INFO
{
    unsigned int Version;      // BSRVDD_PROTOCOL_VERSION
    unsigned int MaxMonitors;  // BSRVDD_MAX_MONITORS
    unsigned int AdapterReady; // 1 = Windows adaptörü hazırladı, monitör eklenebilir
    unsigned int MonitorCount;
} BSRVDD_INFO;

typedef struct BSRVDD_ADD_IN
{
    unsigned int Width;
    unsigned int Height;
    unsigned int RefreshRate; // Hz
    unsigned int Flags;       // protokol 2; eski uygulamalar bu alanı göndermez (12 bayt)
} BSRVDD_ADD_IN;

#define BSRVDD_ADD_IN_V1_SIZE 12
// Aynı boyutta monitör olsa bile yeni bir monitör ekle (Ekran Çoğaltıcı: ör. 2x2 = dört eş monitör).
// Bayrak yoksa aynı boyuttaki mevcut monitör döndürülür (v1.01 ve öncesinin davranışı).
#define BSRVDD_ADD_NEW 0x1

typedef struct BSRVDD_ADD_OUT
{
    unsigned int Id; // 1..BSRVDD_MAX_MONITORS
} BSRVDD_ADD_OUT;

typedef struct BSRVDD_REMOVE_IN
{
    unsigned int Id;
} BSRVDD_REMOVE_IN;

#define BSRVDD_STATE_PENDING  0 // eklenmesi bekleniyor
#define BSRVDD_STATE_ACTIVE   1 // Windows'a bağlandı
#define BSRVDD_STATE_FAILED   2 // eklenemedi (Status alanına bak)
#define BSRVDD_STATE_REMOVING 3

typedef struct BSRVDD_MONITOR_STATE
{
    unsigned int Id;
    unsigned int Width;
    unsigned int Height;
    unsigned int RefreshRate;
    unsigned int State;
    int Status; // NTSTATUS
} BSRVDD_MONITOR_STATE;

#pragma pack(pop)

// Sınırlar
#define BSRVDD_MIN_W   320
#define BSRVDD_MIN_H   200
#define BSRVDD_MAX_W   7680
#define BSRVDD_MAX_H   4320
#define BSRVDD_MIN_HZ  24
#define BSRVDD_MAX_HZ  240
