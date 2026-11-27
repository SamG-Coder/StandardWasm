export const windowsVersionModule = "standardwasm:windows-version";

const infoSize = 276;
const invalidParameter = 0xc000000d | 0;
const defaultVersion = { major: 10, minor: 0, build: 19045 };

export function createWindowsVersionHost(memory, version = defaultVersion) {
    const numbers = [version.major, version.minor, version.build];
    if (numbers.some(value => !Number.isInteger(value) || value < 0 || value > 0x7fffffff))
        throw new RangeError("Invalid Windows guest version.");
    return {
        rtlGetVersion(pointer) {
            const address = pointer >>> 0;
            const buffer = memory.buffer;
            if (address === 0 || address > buffer.byteLength - infoSize) return invalidParameter;
            const info = new DataView(buffer, address, infoSize);
            if (info.getUint32(0, true) !== infoSize) return invalidParameter;
            for (let index = 0; index < numbers.length; index++)
                info.setUint32(4 + index * 4, numbers[index], true);
            info.setUint32(16, 2, true); // VER_PLATFORM_WIN32_NT
            new Uint8Array(buffer, address + 20, infoSize - 20).fill(0);
            return 0;
        }
    };
}
