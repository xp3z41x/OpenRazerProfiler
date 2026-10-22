"""Cliente mínimo do protocolo Razer (feature reports de 90 bytes) para investigação.

Uso como módulo:  from rz import Razer;  kb = Razer();  st, args = kb.get(0x00, 0x81)
"""
import time
import hid

VID, PID = 0x1532, 0x026C
TX_ID = 0x1F

STATUS = {0x00: 'novo', 0x01: 'ocupado', 0x02: 'ok', 0x03: 'falhou', 0x04: 'timeout', 0x05: 'nao suportado'}


def crc(pkt):
    c = 0
    for b in pkt[2:88]:
        c ^= b
    return c


def build(cls, cmd, args=b'', size=None, tx=TX_ID):
    args = bytes(args)
    if size is None:
        size = len(args)
    pkt = bytearray(90)
    pkt[1] = tx
    pkt[5] = size
    pkt[6] = cls
    pkt[7] = cmd
    pkt[8:8 + len(args)] = args
    pkt[88] = crc(pkt)
    return pkt


class RazerError(Exception):
    pass


class Razer:
    def __init__(self, path=None, verbose=False):
        self.verbose = verbose
        self.dev = None
        if path is None:
            path = self._find()
        self.path = path

    def _open(self, path):
        d = hid.device()
        d.open_path(path)
        return d

    def _find(self):
        cands = [d['path'] for d in hid.enumerate(VID, PID)]
        # MI_03 primeiro
        cands.sort(key=lambda p: (b'MI_03' not in p.upper(), p))
        for p in cands:
            try:
                self.dev = self._open(p)
                st, _ = self.xfer(0x00, 0x81, size=2)
                if st == 0x02:
                    return p
            except Exception:
                pass
            try:
                self.dev.close()
            except Exception:
                pass
            self.dev = None
        raise RazerError('interface de controle nao encontrada')

    def close(self):
        if self.dev:
            self.dev.close()
            self.dev = None

    def raw(self, pkt, wait=0.002, tries=60):
        """Envia 90 bytes, devolve a resposta de 90 bytes (repete leitura enquanto ocupado)."""
        self.dev.send_feature_report(bytes([0]) + bytes(pkt))
        time.sleep(wait)
        for _ in range(tries):
            r = self.dev.get_feature_report(0, 91)
            r = bytes(r)
            if len(r) == 91:
                r = r[1:]
            if r[0] != 0x01:
                return r
            time.sleep(0.005)
        return r

    def xfer(self, cls, cmd, args=b'', size=None, tx=TX_ID, wait=0.002):
        pkt = build(cls, cmd, args, size, tx)
        r = self.raw(pkt, wait)
        self.last = r
        if self.verbose:
            print('>>', pkt[:8 + max(len(bytes(args)), 8)].hex(' '))
            print('<<', r[:8 + max(r[5], 8)].hex(' '), STATUS.get(r[0], r[0]))
        if r[0] == 0x02 and (r[6] != cls or r[7] != cmd):
            raise RazerError('resposta trocada: %02x %02x' % (r[6], r[7]))
        return r[0], r[8:8 + 80]

    def get(self, cls, cmd, args=b'', size=None):
        st, a = self.xfer(cls, cmd, args, size)
        return st, a

    def must(self, cls, cmd, args=b'', size=None):
        st, a = self.xfer(cls, cmd, args, size)
        if st != 0x02:
            raise RazerError('%02x %02x -> %s' % (cls, cmd, STATUS.get(st, st)))
        return a
