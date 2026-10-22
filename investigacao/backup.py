"""Backup completo do estado do teclado (somente leituras) em JSON."""
import json, sys, datetime
from rz import Razer

def read_meta(kb, p):
    data = bytearray(); off = 0; total = None
    while True:
        a = kb.must(0x05, 0x88, [p, off >> 8, off & 0xFF], size=0x50)
        assert a[0] == p and ((a[1] << 8) | a[2]) == off, a[:5].hex()
        total = (a[3] << 8) | a[4]
        n = min(75, total - off)
        data += a[5:5 + n]
        off += n
        if off >= total or n <= 0:
            break
    return total, bytes(data)

def read_keymap(kb, p, layer=0):
    keys = {}; start = 0
    while start <= 0xFF:
        a = kb.must(0x02, 0x8F, [p, layer, start], size=0x4A)
        ents = [a[2 + 8 * j: 10 + 8 * j] for j in range(9)]
        got = [e for e in ents if e[0] != 0]
        # fim: entrada "eco" [start, 00...] ou nenhuma entrada
        if not got or (len(got) == 1 and got[0][0] == start and not any(got[0][1:])):
            break
        for e in got:
            keys['%02X' % e[0]] = e.hex()
        start = got[-1][0] + 1
        if len(got) < 9:
            break
    return keys

def main(out):
    kb = Razer()
    hx = lambda a, n: bytes(a[:n]).hex()
    b = {'gerado_em': datetime.datetime.now().isoformat(timespec='seconds'), 'path': kb.path.decode()}
    dev = {}
    dev['firmware'] = hx(kb.must(0x00, 0x81, size=2), 2)
    dev['serial'] = bytes(kb.must(0x00, 0x82, size=0x16)[:0x16]).rstrip(b'\0').decode('ascii', 'replace')
    dev['modo'] = hx(kb.must(0x00, 0x84, size=2), 2)
    dev['polling'] = hx(kb.must(0x00, 0xC0, size=2), 2)
    dev['layout'] = hx(kb.must(0x00, 0x86, size=2), 2)
    for c in (0x83, 0x87):
        st, a = kb.get(0x00, c, size=4)
        dev['00%02X' % c] = {'status': st, 'dados': hx(a, kb.last[5])}
    b['aparelho'] = dev
    prof = {}
    prof['slots'] = kb.must(0x05, 0x80, size=1)[0]
    a = kb.must(0x05, 0x81, size=0x10)
    prof['lista'] = list(a[1:1 + a[0]])
    prof['ativo'] = kb.must(0x05, 0x84, size=1)[0]
    prof['perfis'] = {}
    for p in prof['lista']:
        total, meta = read_meta(kb, p)
        extra = {}
        for c in (0x82, 0x87, 0x95):
            st, a = kb.get(0x02, c, [p], size=0x10)
            extra['02%02X' % c] = bytes(a[:kb.last[5]]).hex() if st == 2 else 'status %d' % st
        st, a = kb.get(0x0F, 0x86, [p, 0x05], size=2)
        extra['0F86'] = bytes(a[:kb.last[5]]).hex() if st == 2 else 'status %d' % st
        prof['perfis'][str(p)] = {'meta_total': total, 'meta': meta.hex(), 'teclas': read_keymap(kb, p),
                                  'teclas_fn': read_keymap(kb, p, 1), 'outros': extra}
    prof['polling_por_perfil'] = {str(p): bytes(kb.must(0x00, 0xC0, [p], size=2)[1:2]).hex() for p in prof['lista']}
    b['perfis'] = prof
    luz = {}
    for s in range(0, 6):
        st, e = kb.get(0x0F, 0x82, [s, 0x05], size=0x0C)
        n = kb.last[5]
        st2, br = kb.get(0x0F, 0x84, [s, 0x05], size=3)
        luz[str(s)] = {'efeito_status': st, 'efeito': hx(e, n), 'brilho_status': st2, 'brilho': br[2]}
    b['iluminacao'] = luz
    mac = {}
    for c in (0x80, 0x8E):
        st, a = kb.get(0x06, c, size=0x10)
        mac['06%02X' % c] = {'status': st, 'dados': hx(a, kb.last[5])}
    b['macros'] = mac
    kb.close()
    with open(out, 'w', encoding='utf-8') as f:
        json.dump(b, f, indent=1, ensure_ascii=False)
    print('backup salvo em', out)
    return b

if __name__ == '__main__':
    main(sys.argv[1])
