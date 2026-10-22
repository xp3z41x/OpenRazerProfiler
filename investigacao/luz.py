"""Mostra a iluminação gravada no perfil ativo e a que está ao vivo (verificação independente)."""
from rz import Razer
kb = Razer()
ativo = kb.must(0x05, 0x84, size=1)[0]
h = lambda s: bytes(kb.must(0x0F, 0x82, [s, 5], size=0x0C)[:kb.last[5]]).hex(' ')
b = lambda s: kb.must(0x0F, 0x84, [s, 5], size=3)[2]
print('perfil ativo %d | gravado: %s brilho %d (%d%%) | ao vivo: %s brilho %d' % (ativo, h(ativo), b(ativo), round(b(ativo) * 100 / 255), h(0), b(0)))
