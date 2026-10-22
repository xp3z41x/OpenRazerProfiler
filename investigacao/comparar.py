"""Compara o estado atual do teclado com um backup JSON (sem gravar nada)."""
import json, sys
from rz import Razer
from backup import read_meta, read_keymap
def comparar(kb, bk):
    difs = []
    b = json.load(open(bk, encoding='utf-8'))
    a = kb.must(0x05, 0x81, size=0x10); lista = list(a[1:1 + a[0]])
    if lista != b['perfis']['lista']: difs.append(('lista', b['perfis']['lista'], lista))
    at = kb.must(0x05, 0x84, size=1)[0]
    if at != b['perfis']['ativo']: difs.append(('ativo', b['perfis']['ativo'], at))
    pol = bytes(kb.must(0x00, 0xC0, size=2)[:2]).hex()
    if pol != b['aparelho']['polling']: difs.append(('polling', b['aparelho']['polling'], pol))
    for p, v in b['perfis']['perfis'].items():
        p = int(p)
        if p not in lista: difs.append(('perfil %d' % p, 'existe', 'ausente')); continue
        t, m = read_meta(kb, p)
        if m.hex() != v['meta']: difs.append(('meta %d' % p, v['meta'][:40], m.hex()[:40]))
        for layer, key in ((0, 'teclas'), (1, 'teclas_fn')):
            km = read_keymap(kb, p, layer)
            for k in sorted(set(km) | set(v[key])):
                if km.get(k) != v[key].get(k): difs.append(('%s %d %s' % (key, p, k), v[key].get(k), km.get(k)))
    for p, code in b['perfis'].get('polling_por_perfil', {}).items():
        if int(p) in lista:
            now = bytes(kb.must(0x00, 0xC0, [int(p)], size=2)[1:2]).hex()
            if now != code: difs.append(('polling perfil %s' % p, code, now))
    for s, v in b['iluminacao'].items():
        s = int(s)
        e = bytes(kb.must(0x0F, 0x82, [s, 5], size=0x0C)[:kb.last[5]]).hex()
        br = kb.must(0x0F, 0x84, [s, 5], size=3)[2]
        if e != v['efeito']: difs.append(('efeito %d' % s, v['efeito'], e))
        if br != v['brilho']: difs.append(('brilho %d' % s, v['brilho'], br))
    return difs
if __name__ == '__main__':
    kb = Razer()
    d = comparar(kb, sys.argv[1])
    print('IGUAL AO BACKUP' if not d else '%d diferenças:' % len(d))
    for x in d: print('  ', x)
