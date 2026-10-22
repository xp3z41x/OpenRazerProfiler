# Formato do backup (JSON)

O backup é gerado por `BackupFile.Serialize` (menu *Exportar backup…* ou `OpenRazerProfiler.exe --exportar arquivo.json`) e lido por `BackupFile.Deserialize`. Os arquivos são codificados em UTF-8 sem BOM.

O backup armazena **os bytes exatos lidos do teclado**, em hexadecimal. Os campos legíveis (`nome`, `efeito`, `hz` e `tipo`) servem apenas para consulta: a importação utiliza somente os campos em hexadecimal, os números de slot e o brilho.

## Estrutura

```json
{
  "formato": "openrazerprofiler-backup",
  "versao": 1,
  "criado_em": "2026-10-22T15:26:09-03:00",
  "aparelho": { "nome": "Razer Huntsman V2", "vid": "1532", "pid": "026C", "firmware": "1.00", "serie": "PM0000X00000000" },
  "polling": { "tipo": "hyperpolling-por-perfil", "hz": 1000, "codigo": "08" },
  "perfil_ativo": 1,
  "perfis": [
    {
      "slot": 1,
      "nome": "default",
      "metadados": "00640065006600610075006c0074",
      "iluminacao": { "efeito": "spectrum", "dados": "03000000", "brilho": 224 },
      "polling": { "tipo": "hyperpolling-por-perfil", "hz": 1000, "codigo": "08" },
      "teclas": { "01": "02020035", "02": "0202001e", "…": "…" },
      "teclas_fn": { "01": "02020035", "…": "…" }
    }
  ]
}
```

| Campo | Conteúdo |
|---|---|
| `formato`, `versao` | Devem ser `openrazerprofiler-backup` e um número menor ou igual a 1; qualquer outro valor é recusado. |
| `aparelho.vid`, `aparelho.pid` | Em hexadecimal. Na importação, devem corresponder ao teclado conectado. |
| `polling` (raiz) | Família de polling do modelo (`tipo`) e taxa em uso no momento do backup. Na família clássica, corresponde à taxa do aparelho; na família por perfil, é apenas informativo. |
| `perfil_ativo` | Slot ativo; deve constar de `perfis`. |
| `perfis[]` | Apenas os slots existentes. Um slot ausente indica slot livre, e a importação desse backup apaga o perfil que estiver nele. |
| `metadados` | Bloco opaco lido por `05 88`: nome em UTF‑16BE no formato de fábrica, ou GUID + nome + hash no formato do Synapse. Não pode ser vazio. |
| `iluminacao.dados` | `[efeito, p1, p2, n, RGB…]`, como na resposta de `0F 82`, sem os dois primeiros bytes ("onde guardar" e LED). |
| `iluminacao.brilho` | De 0 a 255. |
| `polling` (perfil) | Presente apenas na família por perfil: código da taxa daquele perfil. |
| `teclas`, `teclas_fn` | Camadas base e Fn. A chave é o `key_id` em hexadecimal; o valor é a entrada `[tipo, tamanho, dados]` em hexadecimal. Exemplos: `02020035` = tecla comum, sem modificador, usage `0x35`; `0000` = tecla desativada. |

O escritor JSON (`src\Protocolo\Json.cs`) preserva a ordem dos campos e agrupa várias entradas por linha nos mapas de teclas, para manter o arquivo legível.

## Compatibilidade

- Backup sem `polling` dentro dos perfis: cada perfil recebe o `polling` da raiz.
- Backup sem `tipo` no polling (como os gerados antes da introdução da família de polling por modelo): o tipo é ignorado, pois prevalece a família do teclado conectado.
- Os backups gerados pelo script de investigação `investigacao\backup.py`, como o `backup\backup-inicial.json`, usam **outro formato**, lido por `investigacao\comparar.py`. A restauração pela interface ou pelo `TesteAparelho aplicar` utiliza o `backup\backup-original.json`, que contém o mesmo estado no formato do aplicativo. A pasta `backup\` não é versionada; [TESTES.md](TESTES.md) explica como gerar esses dois arquivos.

## Importação

`Session.LoadBackup` (na interface) monta o estado desejado a partir do backup:
- confere o PID e verifica se todos os slots cabem na capacidade do teclado (`05 8A`);
- ajusta o polling à família do modelo conectado: na família por perfil, exige códigos oferecidos pela definição; na clássica, usa o polling da raiz; em modelos sem polling, ignora esse campo;
- substitui perfis, perfil ativo e polling, e todo o conteúdo passa a ser **alteração pendente**.

Na gravação, a comparação é feita **byte a byte** (`KeyBinding.Equals`, e não `SameAction`). Assim, a restauração devolve exatamente o que estava no teclado, inclusive a forma de gravação dos modificadores (usage `E0`–`E7` ou máscara) e a camada Fn, que a interface não edita.
