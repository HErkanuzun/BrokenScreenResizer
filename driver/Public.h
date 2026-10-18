// Ekran Kurtar sanal ekran sürücüsü - uygulama ile sürücü arasındaki ortak tanımlar.
// Aynı değerler C# tarafında src\VirtualDriver.cs içinde de var; birini değiştirirsen diğerini de değiştir.
#pragma once

#include <initguid.h>

// Uygulamanın sürücüyü bulduğu aygıt arayüzü
// {0A6A5C96-5488-4FFF-99CA-E8BD1CB17733}
DEFINE_GUID(GUID_DEVINTERFACE_EKRANKURTAR_VDD,
    0x0a6a5c96, 0x5488, 0x4fff, 0x99, 0xca, 0xe8, 0xbd, 0x1c, 0xb1, 0x77, 0x33);

#define EKVDD_PROTOCOL_VERSION 1
#define EKVDD_MAX_MONITORS     4

// CTL_CODE(FILE_DEVICE_UNKNOWN, fn, METHOD_BUFFERED, FILE_ANY_ACCESS)
#define EKVDD_CTL(fn) (((0x22) << 16) | ((fn) << 2))
#define IOCTL_EKVDD_INFO   EKVDD_CTL(0x900) // çıkış: EKVDD_INFO
#define IOCTL_EKVDD_ADD    EKVDD_CTL(0x901) // giriş: EKVDD_ADD_IN, çıkış: EKVDD_ADD_OUT
#define IOCTL_EKVDD_REMOVE EKVDD_CTL(0x902) // giriş: EKVDD_REMOVE_IN (Id = 0 hepsini kaldırır)
#define IOCTL_EKVDD_LIST   EKVDD_CTL(0x903) // çıkış: EKVDD_MONITOR_STATE dizisi

#pragma pack(push, 4)

typedef struct EKVDD_INFO
{
    unsigned int Version;      // EKVDD_PROTOCOL_VERSION
    unsigned int MaxMonitors;  // EKVDD_MAX_MONITORS
    unsigned int AdapterReady; // 1 = Windows adaptörü hazırladı, monitör eklenebilir
    unsigned int MonitorCount;
} EKVDD_INFO;

typedef struct EKVDD_ADD_IN
{
    unsigned int Width;
    unsigned int Height;
    unsigned int RefreshRate; // Hz
} EKVDD_ADD_IN;

typedef struct EKVDD_ADD_OUT
{
    unsigned int Id; // 1..EKVDD_MAX_MONITORS
} EKVDD_ADD_OUT;

typedef struct EKVDD_REMOVE_IN
{
    unsigned int Id;
} EKVDD_REMOVE_IN;

#define EKVDD_STATE_PENDING  0 // eklenmesi bekleniyor
#define EKVDD_STATE_ACTIVE   1 // Windows'a bağlandı
#define EKVDD_STATE_FAILED   2 // eklenemedi (Status alanına bak)
#define EKVDD_STATE_REMOVING 3

typedef struct EKVDD_MONITOR_STATE
{
    unsigned int Id;
    unsigned int Width;
    unsigned int Height;
    unsigned int RefreshRate;
    unsigned int State;
    int Status; // NTSTATUS
} EKVDD_MONITOR_STATE;

#pragma pack(pop)

// Sınırlar
#define EKVDD_MIN_W   320
#define EKVDD_MIN_H   200
#define EKVDD_MAX_W   7680
#define EKVDD_MAX_H   4320
#define EKVDD_MIN_HZ  24
#define EKVDD_MAX_HZ  240
