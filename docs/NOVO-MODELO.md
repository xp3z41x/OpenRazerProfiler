# Como adicionar outro teclado Razer

Um novo modelo é uma instância de `DeviceDefinition` (como a de `src\Aparelhos\HuntsmanV2.cs`) incluída em `DeviceCatalog.All`. O protocolo e a interface não precisam ser alterados, **desde que** o aparelho use os mesmos comandos do Huntsman V2:
- perfis armazenados na memória, pela classe `05`;
- iluminação pela matriz estendida (`0F 02`/`0F 82`);
- mapa de teclas pela classe `02`.

Modelos com a iluminação antiga (classe `03`) ou sem perfis na memória exigiriam mudanças em `src\Protocolo`.

## 1. Levantamento dos valores (somente leituras)

As leituras podem ser feitas com `investigacao\rz.py` (ajustando `PID` e `TX_ID` no início do arquivo) ou com o `TesteAparelho`. Qualquer gravação deve seguir as orientações de [TESTES.md](TESTES.md).

| Valor | Como obter |
|---|---|
| PID | `hid.enumerate(0x1532)` em Python, ou `Get-PnpDevice` filtrando por `VID_1532`. |
| Interface de controle | A interface que responde com sucesso ao comando `00 81`. O `DeviceCatalog.Connect` testa todas, começando pela `ControlInterface`. |
| Transaction id | `0x1F` no Huntsman V2; segundo o OpenRazer, outros modelos usam `0xFF` ou `0x3F`. O `00 81` deve ser testado com cada um deles. |
| LED | `0F 82 [0x00, led]` para valores de led entre `0x00` e `0x1F`: os que respondem com sucesso existem (o backlight costuma ser o `0x05`). |
| Efeitos | `0F 81 [led]` → `[led, ids…]`. |
| Limites dos efeitos | Cada parâmetro deve ser gravado **na RAM** (`onde = 0x00`, sem persistência) e relido: o firmware responde `03` aos valores que recusa. A direção e o sentido da velocidade somente podem ser determinados observando o teclado. |
| Slots de perfil | `05 8A` (capacidade). O `05 80` conta apenas os perfis gravados. |
| Família de polling | `00 C0 [0]` com sucesso → `HyperPollingPerProfile`; `00 85` com sucesso → `Classic`; nenhum dos dois → `None`. Para 8000 Hz, o aparelho precisa negociar USB High Speed. |
| Mapa de fábrica | Leitura do perfil de fábrica (normalmente o 1) com `TesteAparelho ler saida.json`, seguida da conversão indicada abaixo. |
| Modelo de perfil novo | O que o firmware grava no slot com `05 02`. Exige gravação em um slot de teste, com backup e restauração. |

Para converter um backup do aplicativo nas tabelas de `KeyTable` (pares `id:entrada` separados por espaço):

```python
import json
b = json.load(open('saida.json', encoding='utf-8'))
p = next(p for p in b['perfis'] if p['slot'] == 1)
base = ' '.join(f'{k}:{v}' for k, v in p['teclas'].items())
fn = ' '.join(f'{k}:{v}' for k, v in p['teclas_fn'].items() if p['teclas'].get(k) != v)
print(base)
print(fn)   # apenas o que difere na camada Fn
```

## 2. Escrita da definição

| Campo | Huntsman V2 | Observação |
|---|---|---|
| `Name`, `ProductId`, `VendorId` | `"Razer Huntsman V2"`, `0x026C`, `0x1532` | |
| `ControlInterface`, `TransactionId`, `BacklightLed` | `3`, `0x1F`, `0x05` | |
| `Effects` | desligado, estático, breathing, spectrum, wave, reactive, starlight | IDs de `EffectId`, na ordem em que os botões aparecem. |
| `WaveLeftToRight`, `WaveRightToLeft` | `0x01`, `0x02` | Confirmados visualmente. |
| `WaveSpeeds`, `WaveDefaultSpeed` | `80 60 48 38 28 20 18 10 08`, `0x28` | Bytes ordenados do mais lento ao mais rápido; o valor padrão deve constar da lista. |
| `ReactiveSpeedMin/Max`, `StarlightSpeedMin/Max` | 1–4, 1–3 | Exibidos na interface como "Duração", de curta a longa. |
| `Polling`, `PollingRates` | `HyperPollingPerProfile`, 125–8000 Hz | Taxas em Hz; cada uma deve existir na tabela da família. |
| `Layout` | `LayoutBuilder` | Em unidades de tecla: `Row(y)`, `At(x)`, `Gap(w)`, `Key(id, rótulo, largura, altura)` e `KeyAt(...)` para posições livres. |
| `DefaultKeys`, `DefaultFnKeys` | `KeyTable.Parse(...)` | Toda tecla do `Layout` precisa ter uma ação de fábrica. Teclas que existem no mapa, mas não no desenho (ISO/JIS no layout US), permanecem intocadas. |
| `FixedKeys` | `{ 0x3B }` (Fn) | Teclas que não podem ser remapeadas pela interface. |
| `NewProfileLighting`, `NewProfileBrightness`, `NewProfilePollingCode` | spectrum `6EDAFD`, `0xFF`, `0x01` | O que o firmware grava com `05 02`. As teclas do perfil novo derivam do mapa de fábrica, com modificadores em forma de máscara (`NewProfileTemplate`). |

## 3. Validação

- `DeviceDefinition.Validate()` é executado ao abrir o aplicativo (`DeviceCatalog.CheckDefinitions`) e recusa definições incoerentes, como uma taxa inexistente na família de polling, uma tecla sem ação de fábrica ou uma velocidade padrão fora da lista.
- `TesteAparelho validar` faz essa conferência sem gravar nada no teclado.
- Em seguida, a definição deve passar pelo procedimento de [TESTES.md](TESTES.md), e o que for validado ou permanecer como hipótese deve ser registrado em [PROTOCOLO.md](PROTOCOLO.md).
