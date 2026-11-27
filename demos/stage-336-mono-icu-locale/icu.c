#include <stdint.h>

extern void udata_setCommonData(const void* data, int32_t* error);

int32_t installIcuData(const void* data)
{
    int32_t error = 0;
    udata_setCommonData(data, &error);
    return error;
}
