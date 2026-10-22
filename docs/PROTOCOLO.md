# Protocolo do Razer Huntsman V2 (memória interna)

Este documento descreve os comandos e os formatos de dados usados pelo aplicativo, indicando o que foi validado no aparelho e o que permanece como hipótese. As informações foram conferidas em 22/10/2026, em um Huntsman V2 com layout US, PID `0x026C` e firmware 1.00.

Neste documento, **validado** significa que o comando foi enviado e que a releitura confirmou o resultado. A coluna "Como" das tabelas indica o tipo de verificação:

| Sigla | Significado |
|---|---|
| **R** | somente leitura |
| **G+R** | gravado e relido |
| **V** | conferido visualmente no teclado |
| **D** | persistiu após desconectar e reconectar o cabo |

## Transporte (validado)

- Dispositivo USB `1532:026C`. A interface de controle é a **MI_03** (usage page `0x000C`, feature report de 91 bytes), que pode ser aberta sem privilégios de administrador. O aplicativo escolhe a interface em que o comando `00 81` responde com sucesso.
- Cada pacote é formado pelo report ID 0 seguido de 90 bytes e enviado com `HidD_SetFeature`; após cerca de 2 ms, a resposta é lida com `HidD_GetFeature`.
- A resposta repete os primeiros argumentos do pedido (perfil, offset…). O aplicativo usa esse eco para descartar respostas antigas.
- O bit `0x80` no byte de comando indica leitura (GET); o mesmo comando sem esse bit é uma gravação (SET).
- **"Onde guardar"** (primeiro argumento dos comandos de iluminação **e de polling**): `0x00` = ao vivo (RAM); `0x01`–`0x05` = perfis 1 a 5.
- **Não existe comando de "salvar"**: toda gravação em perfil vai diretamente para a memória permanente (**D**). Gravar no perfil **ativo** também atualiza o estado ao vivo (iluminação e polling).
- Cada leitura ou gravação leva cerca de 3 ms; a leitura completa do teclado leva cerca de 0,5 s.

Estrutura dos 90 bytes do pacote:

| Byte(s) | Conteúdo |
|---|---|
| 0 | status |
| 1 | transaction id (`0x1F`) |
| 2–3 | pacotes restantes |
| 4 | protocolo |
| 5 | tamanho dos argumentos |
| 6 | classe do comando |
| 7 | comando |
| 8–87 | argumentos |
| 88 | CRC (XOR dos bytes 2 a 87) |
| 89 | 0 |

Valores de status:

| Status | Significado |
|---|---|
| `01` | ocupado (a resposta deve ser lida novamente) |
| `02` | sucesso |
| `03` | falha |
| `04` | tempo esgotado |
| `05` | não suportado |

## Comandos

| Comando (GET / SET) | Argumentos | Resposta / efeito | Como |
|---|---|---|---|
| `00 81` | — | Versão do firmware `[maior, menor]` (`01 00` = 1.00) | R |
| `00 82` | tamanho `0x16` | Número de série em ASCII | R |
| `00 84` / `00 04` | SET `[modo, 00]` | Modo do aparelho: `00` normal, `03` driver (a iluminação é controlada por software) | G+R |
| `00 86` | — | Layout `[01 80]` (`01` = US) | R |
| `00 C0` / `00 40` | `[onde]` / `[onde, código]` | Polling **por perfil** (família HyperPolling; ver adiante) | G+R |
| `05 80` | — | **Quantidade de perfis gravados** (e não de slots) | R |
| `05 8A` | — | **Capacidade de slots** (`05`) | R |
| `05 81` | — | `[n, ids…]`: perfis existentes | G+R |
| `05 84` / `05 04` | SET `[perfil]` | Perfil ativo; ativar um perfil carrega a iluminação, as teclas e o polling dele | G+R+V |
| `05 88` / `05 08` | `[perfil, off_hi, off_lo]` / `[perfil, off(2), total(2), até 75 B]` | Metadados, em blocos de 75 B; o total pode ser alterado | G+R |
| `05 02` | `[perfil]` | **Cria** o perfil (ou reinicia um perfil existente) com o modelo do firmware | G+R |
| `05 03` | `[perfil]` | **Apaga** o perfil: ele deixa de constar em `05 81`, e `05 88` passa a falhar | G+R |
| `0F 81` | `[led]` | `[led, efeitos…]` = `00 01 02 03 04 05 07 08` | R |
| `0F 82` / `0F 02` | `[onde, led]` / `[onde, led, efeito, p1, p2, n, RGB…]` | Efeito de iluminação; tamanho = 6 + 3n | G+R+V |
| `0F 84` / `0F 04` | `[onde, led]` / `[onde, led, brilho]` | Brilho, de 0 a 255 | G+R+V |
| `02 8F` | `[perfil, camada, tecla_inicial]` | 9 entradas de 8 B `[id, tipo, tam, dados(5)]`; camada 0 = base, 1 = Fn; perfil 0 = mapa em uso | R |
| `02 83` / `02 03` | `[perfil, tecla]` / `[perfil, tecla, tipo, tam, dados]` | Uma tecla da camada base | G+R |
| `02 8D` / `02 0D` | `[perfil, tecla, camada]` / `[perfil, tecla, camada, tipo, tam, dados]` | Uma tecla de qualquer camada (forma usada pelo aplicativo) | G+R |
| `06 8E` | — | Memória: `FF FF` + total de 262128 B, 260728 B disponíveis ao usuário e espaço livre (u32 BE) | R |
| `06 80` | — | Macros gravadas (`00 00`) | R |

LEDs: apenas o `0x05` (backlight) e o `0x00` respondem; o `0x00` devolve os mesmos valores do `0x05`.

## Taxa de polling por família de modelo

Cada definição de aparelho declara a família de comandos de polling e as taxas oferecidas. Ao abrir o aplicativo, uma definição que ofereça uma taxa inexistente na sua família é recusada.

| Família | Comandos | Códigos | Armazenamento | Situação |
|---|---|---|---|---|
| HyperPolling por perfil | `00 C0 [onde]` / `00 40 [onde, código]` | `01`=8000, `02`=4000, `04`=2000, `08`=1000, `10`=500, `20`=250, `40`=125 | em cada perfil | **validada** no Huntsman V2 |
| clássica | `00 85` / `00 05 [código]` | `01`=1000, `02`=500, `08`=125 | uma taxa para o aparelho | descrita pelo OpenRazer; **não testada** (o Huntsman V2 responde "não suportado" a `00 85`) |
| nenhuma | — | — | — | o aplicativo não lê nem grava o polling e oculta o controle |

A taxa de 8000 Hz do Huntsman V2 foi conferida no barramento USB: o teclado negocia **High Speed (480 Mb/s)**, e as 5 interfaces HID possuem endpoint de interrupção com `bInterval = 1`, ou seja, o computador consulta o teclado a cada 125 µs. Quando a conexão passa por um hub ou KVM que opera apenas em Full Speed (12 Mb/s), o intervalo mínimo sobe para 1 ms, e taxas acima de 1000 Hz deixam de ter efeito.

## Formatos de dados (validados)

**Efeitos** (`0F 02`). Todos foram gravados pelo aplicativo, relidos e **confirmados visualmente** (resultados no fim deste documento):

| Efeito | id | p1 | p2 | Cores | Limites do firmware |
|---|---|---|---|---|---|
| desligado | `00` | 0 | 0 | 0 | — |
| estático | `01` | 0 | 0 | 1 | — |
| breathing | `02` | modo: `01` uma cor, `02` duas cores, `03` aleatório | 0 | 1, 2 ou 0 | aceita p2 de 1 a 4 (efeito visual não verificado) |
| spectrum | `03` | 0 | 0 | 0 | — |
| wave | `04` | **`01` esquerda → direita, `02` direita → esquerda** | velocidade, em que **menor = mais rápido**; `28` = normal | 0 | recusa direção 0 e 3; aceita qualquer velocidade |
| reactive | `05` | 0 | duração do brilho: **de `01` (curta) a `04` (longa, ~2 s)** | 1 | recusa 0 e valores a partir de 5 |
| starlight | `07` | 0 | duração das estrelas: **de `01` (curta) a `03` (longa)**; cada estrela varia | 0, 1 ou 2 | recusa 0 e valores a partir de 4 |

**Metadados** (bloco opaco, que o firmware não interpreta):
- formato de fábrica (perfil 1): nome em UTF‑16BE (`default`, 14 B). O aplicativo usa esse formato nos perfis que cria;
- formato do Synapse: GUID (16 B) + nome em UTF‑8 completado com zeros (100 B) + hash hexadecimal (64 B) + zeros, totalizando 250 B. Ao renomear, o aplicativo altera apenas o nome e mantém o GUID e o hash.

**Mapa de teclas**, com entradas no formato `[tipo, tam, dados]`:

| Tipo | Tamanho | Significado |
|---|---|---|
| `00` | 0 | **Tecla desativada.** Gravada e relida; no teste funcional, a tecla não gerou nenhum evento no Windows. |
| `02` | 2 | `[máscara, usage]` de teclado. Máscara: bit 0 LCtrl, 1 LShift, 2 LAlt, 3 LWin, 4 RCtrl, 5 RShift, 6 RAlt, 7 RWin. A fábrica grava os modificadores como usage `E0`–`E7` (exceto o LWin, que vem como máscara `08`); o firmware e o Synapse os gravam como máscara. Os dois formatos funcionam. |
| `09` | 1 | Generic Desktop / System Control (`82` = suspender; é o Fn+Pause de fábrica). |
| `0A` | 2 | Consumer, 16 bits big-endian (mídia: `E2` mudo, `E9`/`EA` volume, `CD` reproduzir/pausar, `B5`/`B6` próxima/anterior). |
| `11` | 1 | Função do firmware (`01` = Fn). |
| `07` | 1 | Função do firmware (Fn+Menu de fábrica = `07 04`; significado desconhecido). |

**Leitura do mapa.** O pedido devolve as teclas com id maior ou igual ao inicial, pulando as que não existem. Após a última tecla (`CE`), a resposta é apenas `[início, 00…]`, idêntica à de uma tecla desativada. Para distinguir os dois casos, utiliza-se a leitura individual (`02 8D`), que **falha** para ids inexistentes.

**Modelo do firmware para perfil novo** (`05 02`): efeito spectrum com a cor `6E DA FD` armazenada, brilho 255 e mapa de fábrica com modificadores em forma de máscara (nas duas camadas). O `05 02` **não altera** os metadados nem o polling: ambos mantêm o que havia no slot.

## Comportamentos observados

- O código de polling não é validado pelo firmware, que aceitou e armazenou o código `03`. O aplicativo envia apenas códigos da tabela.
- O polling é armazenado **em cada perfil**. Os perfis 2 a 5 (criados pelo modelo do firmware) estavam a 8000 Hz e o perfil 1 a 1000 Hz, e trocar o perfil ativo alterava a taxa. O aplicativo exibe uma única taxa e a grava em todos os perfis.
- Apagar um perfil não limpa os dados: a iluminação e as teclas continuam legíveis no slot vazio.
- O firmware usa os tamanhos internos das entradas: uma tecla enviada com o campo de tamanho dos dados do pacote 1 byte menor foi, ainda assim, gravada corretamente.

## Hipóteses e itens não confirmados

- **Camada Fn de fábrica**: F9 `11/04`, F10 `11/03`, F11 `11/09`, F12 `11/08`, Pause `09/82`, Menu `07/04`. Pela posição, devem corresponder a gravar macro, modo jogo, brilho −, brilho + e suspender; apenas a do Pause tem significado confirmado.
- **Teclas internas `A0`–`A4` e `A9`–`AF`**: tipo `11`, valores `20` a `2B`. Permanecem intocadas.
- **Teclas ISO/JIS no mapa do layout US**: `0E` ¥, `2A` #, `2D` \\, `38` Ro, `6B` Num ,, `96`–`9A` (teclas japonesas). Permanecem intocadas.
- Efeitos `06` (não listado) e `08` (quadro personalizado): aceitos pelo firmware, mas não usados.
- Leituras que respondem, mas cujo significado é desconhecido: `00 83` (`01 34`), `00 87` (`01 00 05 00`), `0F 80` (`05 19 03 06 16`; talvez LED 5 e matriz 6×22), `0F 86 [onde, led]` (`[onde, 01]`), `0F 90` (`01`), `02 82 [perfil]` (`00 00 00 00`), `02 87 [perfil]` (`[perfil, 01]`), `02 95 [perfil]` (`[perfil, 00 14 00 28]`), `03 89` (`02 07 01 08 01`), `06 81` e `06 86`–`06 8D`. Com argumentos zerados, `02 83`, `02 8D`, `03 80`–`03 84` e `05 88` respondem "falha".
- Itens deliberadamente não testados: gravação da camada Fn pela interface, macros (classe `06`) e exclusão do perfil ativo. O aplicativo não permite apagar o perfil ativo.

## Testes de aceitação do aplicativo (22/10/2026)

Cada item foi gravado pelo `OpenRazerProfiler.exe`, conferido pela releitura do próprio aplicativo e, depois, pelo script independente `investigacao\comparar.py`.

| Recurso | Procedimento | Resultado |
|---|---|---|
| Perfis | renomear (com acento), apagar, criar em um slot livre, ativar | ok |
| Remapeamento (perfil 5) | Caps Lock → Ctrl; PrtSc → Ctrl+Shift+F14; Scroll Lock → F13; Pause → Volume +; Menu → desativada | ok; **teste funcional**: o Windows recebeu Ctrl esquerdo, Ctrl+Shift+F14, F13 e Volume +, e nenhum evento para a tecla Menu |
| Polling | 500 Hz e, em seguida, 1000 Hz em todos os perfis | ok |
| Backup | exportar (resultado idêntico ao backup feito pelo script Python) e importar (restaurou o teclado) | ok |
| Avisos | Synapse em execução (simulado por um processo de teste) e modo driver (com retorno ao modo normal) | ok |

Confirmação visual da iluminação, com todos os efeitos aplicados ao perfil ativo:

| Rodada | Gravado | Observado no teclado |
|---|---|---|
| 1 | estático `FF0000`, brilho 50% | vermelho fixo, brilho médio |
| 2 | breathing de duas cores (`FF0000`, `0078FF`), brilho 100% | respiração alternando as duas cores |
| 3 | wave com direção `02` e velocidade `10` | da direita para a esquerda, rápida (antes: direção `01` = da esquerda para a direita; `28` = normal) |
| 4 | reactive `FFFF00`, duração `04` e depois `01` | teclas acendem em amarelo; cerca de 2 s com `04` e mais rápido com `01` |
| 5 | starlight de duas cores (`FFFF00`, `0078FF`), duração `03` e depois `01` | estrelas nas duas cores; com `01`, somem mais rápido |
| 6 | desligado | toda a iluminação apagada |
| 7 | spectrum, brilho 88% (restauração pelo backup) | ciclo de cores |

Ao final, o teclado foi restaurado pela opção **Importar backup** e ficou idêntico ao backup feito antes de qualquer gravação.
