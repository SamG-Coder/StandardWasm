const noAccess = 998, filenameTooLong = 206;
export const maximumUnits = 32766; // UNICODE_STRING content; leave room for NUL.
export const deviceComponent = /^(?:CON|PRN|AUX|NUL|CLOCK\$|COM[1-9¹²³]|LPT[1-9¹²³])(?:[. ]|$)/iu;

export function readPath(view, address) {
    if (address === 0) return { error: noAccess };
    let path = "";
    for (let index = 0; index <= maximumUnits; index++) {
        const offset = address + 2 * index;
        if (offset > view.byteLength - 2) return { error: noAccess };
        const unit = view.getUint16(offset, true);
        if (unit === 0) return { path, bytes: 2 * (index + 1) };
        path += String.fromCharCode(unit);
    }
    return { error: filenameTooLong };
}
