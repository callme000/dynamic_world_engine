import struct

with open(r"C:\Program Files\Epic Games\GTAVEnhanced\scripts\EnableAllinteriors.dll", "rb") as f:
    data = f.read()

# Locate PE header
pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
# Optional header magic
magic = struct.unpack_from("<H", data, pe_offset + 0x18)[0]
# CLI header RVA is at offset 0xE8 (PE32+) or 0xD8 (PE32) from Optional Header
cli_rva_offset = pe_offset + 0x18 + (0x70 if magic == 0x10B else 0x80) + 14 * 8
cli_rva, cli_size = struct.unpack_from("<II", data, cli_rva_offset)
print(f"CLI RVA: {hex(cli_rva)}, Size: {cli_size}")

# Section headers
num_sections = struct.unpack_from("<H", data, pe_offset + 0x6)[0]
opt_hdr_size = struct.unpack_from("<H", data, pe_offset + 0x14)[0]
sec_offset = pe_offset + 0x18 + opt_hdr_size

def rva_to_offset(rva):
    for i in range(num_sections):
        s_off = sec_offset + i * 40
        vsize, vrva, raw_size, raw_off = struct.unpack_from("<IIII", data, s_off + 8)
        if vrva <= rva < vrva + vsize:
            return raw_off + (rva - vrva)
    return None

cli_off = rva_to_offset(cli_rva)
metadata_rva = struct.unpack_from("<I", data, cli_off + 8)[0]
metadata_off = rva_to_offset(metadata_rva)
print(f"Metadata offset: {hex(metadata_off)}")

# Metadata root: Magic 0x424A5342 (BSJB)
magic = struct.unpack_from("<I", data, metadata_off)[0]
print(f"Metadata magic: {hex(magic)}")
version_len = struct.unpack_from("<I", data, metadata_off + 12)[0]
streams_offset = metadata_off + 16 + version_len + (4 - (version_len % 4) if version_len % 4 != 0 else 0) + 2
num_streams = struct.unpack_from("<H", data, streams_offset)[0]
cur = streams_offset + 2

us_offset = None
us_size = None
for _ in range(num_streams):
    s_rva, s_size = struct.unpack_from("<II", data, cur)
    cur += 8
    name = []
    while data[cur] != 0:
        name.append(chr(data[cur]))
        cur += 1
    cur += 1
    # align to 4
    if cur % 4 != 0:
        cur += 4 - (cur % 4)
    s_name = "".join(name)
    print(f"Stream: {s_name}, offset: {hex(metadata_off + s_rva)}, size: {s_size}")
    if s_name == "#US":
        us_offset = metadata_off + s_rva
        us_size = s_size

if us_offset:
    print(f"\n--- DUMPING USER STRINGS (#US) [size: {us_size}] ---")
    pos = us_offset + 1 # skip first null byte
    end = us_offset + us_size
    user_strings = []
    while pos < end:
        # read 7-bit encoded length
        b = data[pos]
        pos += 1
        if b & 0x80 == 0:
            length = b
        elif b & 0xC0 == 0x80:
            length = ((b & 0x3F) << 8) | data[pos]
            pos += 1
        else:
            length = ((b & 0x1F) << 24) | (data[pos] << 16) | (data[pos+1] << 8) | data[pos+2]
            pos += 3
        if length == 0:
            continue
        try:
            # Last byte is a flag in #US
            s = data[pos:pos+length-1].decode("utf-16le", errors="ignore")
            user_strings.append(s)
        except Exception:
            pass
        pos += length

    print(f"Total user strings: {len(user_strings)}")
    with open("user_strings.txt", "w", encoding="utf-8") as out:
        for s in user_strings:
            out.write(s + "\n")
    print("Saved to user_strings.txt")
