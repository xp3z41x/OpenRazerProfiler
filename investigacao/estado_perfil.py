"""Lê o estado completo de um perfil (para comparar antes/depois)."""
from backup import read_meta, read_keymap
def estado(kb, p):
    e = {}
    st, a = kb.get(0x05, 0x81, size=0x10); e['lista'] = bytes(a[:1 + a[0]]).hex(' ')
    e['ativo'] = kb.must(0x05, 0x84, size=1)[0]
    st, a = kb.get(0x05, 0x88, [p, 0, 0], size=0x50); e['meta_st'] = st
    if st == 2:
        t, m = read_meta(kb, p); e['meta'] = m.hex()
    st, a = kb.get(0x0F, 0x82, [p, 5], size=0x0C); e['luz'] = (st, bytes(a[:kb.last[5]]).hex(' '))
    st, a = kb.get(0x0F, 0x84, [p, 5], size=3); e['brilho'] = (st, a[2])
    try:
        e['teclas'] = read_keymap(kb, p); e['teclas_fn'] = read_keymap(kb, p, 1)
    except Exception as ex:
        e['teclas'] = 'erro: %s' % ex
    return e
def diff(a, b):
    for k in sorted(set(a) | set(b)):
        if a.get(k) != b.get(k):
            va, vb = a.get(k), b.get(k)
            if isinstance(va, dict) and isinstance(vb, dict):
                d = {x: (va.get(x), vb.get(x)) for x in set(va) | set(vb) if va.get(x) != vb.get(x)}
                print('  %s: %d diferenças %s' % (k, len(d), dict(list(d.items())[:8])))
            else:
                print('  %s: %s -> %s' % (k, str(va)[:100], str(vb)[:100]))
