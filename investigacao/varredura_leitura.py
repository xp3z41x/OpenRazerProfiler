"""Varre comandos de LEITURA (bit 0x80) e registra quais respondem."""
import json, sys
from rz import Razer, STATUS
kb = Razer()
res = {}
classes = range(0x00, 0x11)
for cls in classes:
    for cmd in range(0x80, 0x100):
        st, a = kb.xfer(cls, cmd, b'', size=0x50)
        r = kb.last
        if st != 0x05:
            n = r[5]
            res['%02X %02X' % (cls, cmd)] = {'status': STATUS.get(st, st), 'tam': n, 'dados': bytes(a[:max(n, 0)]).hex(' ')}
kb.close()
json.dump(res, open(sys.argv[1], 'w'), indent=1)
for k, v in res.items():
    print(k, v['status'], v['tam'], v['dados'][:120])
