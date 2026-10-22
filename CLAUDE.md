# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

O OpenRazerProfiler é um aplicativo portátil para Windows 11 que lê e grava **diretamente na memória do teclado** Razer Huntsman V2 (USB `1532:026C`, interface de controle MI_03): os 5 slots de perfil, a iluminação do próprio firmware, o remapeamento de teclas, a taxa de polling e o backup em JSON. O resultado é um único `.exe` WinForms para o .NET Framework 4.8, já incluído no Windows 11, sem instalador, sem serviço e sem nenhum processo em segundo plano.

A interface, a documentação e os comentários são escritos em português do Brasil; os identificadores do código, em inglês. O mantenedor do projeto escreve em português. O [README](README.md) apresenta o projeto ao público geral; a documentação técnica está em [docs/](docs/).

## Decisões

Quando uma decisão couber mais ao usuário do que ao assistente, o usuário deve ser consultado antes de prosseguir, com pedido de esclarecimento quando necessário. Exemplos: comportamento ou aparência da interface, alteração de algo que o usuário especificou e qualquer operação no teclado além do combinado.

## Comandos

```powershell
# Compilação: gera dist\OpenRazerProfiler.exe. O aplicativo deve estar fechado, pois com ele em execução a cópia do executável falha.
powershell -ExecutionPolicy Bypass -File build.ps1

# Programa de teste em console: compila src\Protocolo e src\Aparelhos e opera sobre o teclado real
dotnet build testes\TesteAparelho\TesteAparelho.csproj -c Release
testes\TesteAparelho\bin\Release\TesteAparelho.exe validar      # somente leituras; o equivalente mais próximo de uma suíte de testes
testes\TesteAparelho\bin\Release\TesteAparelho.exe ler [saida.json]
testes\TesteAparelho\bin\Release\TesteAparelho.exe comparar <backup.json>
testes\TesteAparelho\bin\Release\TesteAparelho.exe aplicar <backup.json>   # GRAVA no teclado
testes\TesteAparelho\bin\Release\TesteAparelho.exe ativar <slot>           # GRAVA no teclado

# Conferência independente em Python (requer pip install hidapi), executada a partir de investigacao\
cd investigacao; python comparar.py ..\backup\backup-inicial.json   # o resultado esperado é IGUAL AO BACKUP

# Backup sem abrir a janela
dist\OpenRazerProfiler.exe --exportar arquivo.json

# Regeneração de src\Interface\app.ico (o desenho do olho está definido no próprio script)
powershell -ExecutionPolicy Bypass -File ferramentas\gerar-icone.ps1
```

Não há lint nem testes unitários: toda a validação é feita no aparelho, conforme [docs/TESTES.md](docs/TESTES.md).

A pasta `backup\` não é versionada, pois os arquivos contêm o número de série do aparelho. `backup-inicial.json` e `backup-original.json` são os backups locais do estado original do teclado, criados antes de qualquer gravação; [docs/TESTES.md](docs/TESTES.md) explica como gerá-los.

## Arquitetura

O código tem três partes, com dependência em um único sentido: `src\Interface` → `src\Aparelhos` → `src\Protocolo`.

- **Protocolo** não conhece modelos específicos. Contém o transporte HID por feature reports de 90 bytes, a classe `RazerKeyboard` (um método por comando), o modelo de estado (`DeviceState`), o cálculo de diferenças (`StateDiff`), a sincronização (`DeviceSync`) e o backup em JSON. O que é específico de cada modelo (transaction id, LED e família de polling) é recebido pelo construtor de `RazerKeyboard`.
- **Aparelhos** contém os dados de cada modelo (`DeviceDefinition`; atualmente, apenas `HuntsmanV2.Definition`) e o `DeviceCatalog`, que valida as definições e localiza a interface HID em que o comando `00 81` responde. Adicionar um modelo consiste em criar uma nova definição e incluí-la em `DeviceCatalog.All` ([docs/NOVO-MODELO.md](docs/NOVO-MODELO.md)).
- **Interface** é a camada WinForms. A `Session` mantém `Device` (o último estado lido) e `Working` (as edições); as pendências são calculadas por `StateDiff.Compare(Device, Working)`.

Fluxo: `DeviceCatalog.Connect()` → `DeviceSync.ReadAll()` → o usuário edita o `Working` → *Gravar no teclado* chama `DeviceSync.Save()`, que grava apenas as diferenças, relê todo o estado, compara novamente e repete uma vez o que não tiver sido confirmado.

Conceitos que atravessam vários arquivos:
- **"Onde guardar"**: primeiro argumento dos comandos de iluminação e de polling. `0` = ao vivo (RAM, perdido ao desconectar); `1`–`5` = perfis. Gravar em um perfil já é permanente (não existe comando de "salvar"), e gravar no perfil ativo também altera o estado ao vivo. A prévia da interface grava somente na RAM, e `Session.EndPreview` restaura o valor gravado.
- **Comparação exata × normalizada**: diferenças, gravação, verificação e backup usam igualdade byte a byte (`KeyBinding.Equals`), para que a restauração reproduza exatamente o estado anterior. Somente a interface usa `KeyBinding.SameAction`, que trata um modificador como usage `E0`–`E7` ou como bit da máscara indistintamente, para decidir se uma tecla está "no padrão".
- **Criação de perfil** (`05 02`): reinicia iluminação, brilho e teclas com o modelo do firmware (`DeviceDefinition.NewProfileTemplate`), mas preserva os metadados e o polling que já estavam no slot; por isso, `Apply` sempre regrava esses dois itens. A **exclusão** (`05 03`) apenas remove o slot da lista.
- **Polling por família** (`PollingKind`): o Huntsman V2 armazena a taxa em cada perfil (HyperPolling, até 8000 Hz). A interface exibe um único controle e grava o valor em todos os perfis.

Detalhes em [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Restrições do código

- `OpenRazerProfiler.csproj` e `testes\TesteAparelho\TesteAparelho.csproj` usam `<Compile Include>` explícitos (`EnableDefaultCompileItems=false`). Código novo deve ficar em uma das três pastas de `src\`.
- `Protocolo` e `Aparelhos` não podem usar WinForms nem `System.Drawing`, pois o `TesteAparelho` os compila como aplicativo de console.
- `LangVersion latest` sobre net48 permite apenas recursos de sintaxe. `record`/`init`, membros padrão em interfaces e `Index`/`Range` não estão disponíveis.
- O firmware aceita valores inválidos sem sinalizar erro (por exemplo, o código de polling `0x03`). Os valores devem ser validados antes do envio, como já fazem `RazerKeyboard` e `DeviceDefinition.Validate()`.
- O aplicativo é um arquivo único: sem `app.config` (o DPI é declarado no `app.manifest`) e sem dependências NuGet em tempo de execução (o JSON é implementado no próprio projeto, em `Json.cs`).

## Interface

- **Sem frases explicativas na tela.** Dicas de funcionamento, avisos sobre o que está pendente, descrições óbvias de cada opção, notas do tipo "a mudança só vale ao gravar" e tooltips que repetem o rótulo não devem ser incluídos. Esse tipo de texto não ajuda quem usa o aplicativo, polui a tela e dá aparência de conteúdo gerado automaticamente ("AI slop"). O estado deve aparecer no próprio controle: botão habilitado ou desabilitado, ponto verde de pendência, cor da tecla, item marcado. Na tela entram apenas nomes, rótulos, erros e avisos que exigem uma ação do usuário (Synapse em execução, Iluminação dinâmica ligada, modo driver). Explicações pertencem a [docs/USO.md](docs/USO.md).
- **Controles próprios** (`Controls.cs`, `Menus.cs`, `SearchPicker.cs`) substituem ComboBox, CheckBox, RadioButton, TrackBar, caixas de texto com borda nativa e menus nativos. Rótulos são sempre criados por `Theme.Label`, que desenha o texto sem o recuo interno do Windows. Código novo deve seguir o mesmo caminho, para não misturar os dois estilos visuais.
- **Medidas:** margem de 16 px na janela (cabeçalho, avisos, cartões e rodapé começam na mesma coluna); conteúdo das páginas a 20 px da borda interna do cartão; controles com 32 px de altura e raio 6; cartões e menus com raio 8. Após qualquer alteração de layout, o resultado deve ser conferido em uma captura ampliada, com medição, e não apenas visualmente.
- **Texto dentro de uma forma** (botão, etiqueta, tecla, círculo) deve passar por `Theme.DrawCenteredText` (ou `DrawCenteredGlyphs`), nunca por `TextFormatFlags.HorizontalCenter`: a caixa da fonte deixa o texto até 1,5 px fora do centro. Todo desenho suavizado começa com `Theme.Smooth`, para que as formas fiquem na mesma grade de pixels do texto.

## Regras para operações no teclado

Estas regras fazem parte dos requisitos do projeto desde o início:
- **Antes de qualquer gravação na memória permanente, deve-se fazer um backup completo e, ao final, restaurá-lo** (`backup\backup-original.json` pela interface, ou `TesteAparelho aplicar`), com conferência do resultado por `comparar.py`, a menos que o usuário peça para manter alguma alteração.
- Comandos de leitura podem ser varridos livremente. Comandos de escrita devem ser enviados um de cada vez, a partir de uma hipótese e seguidos de releitura. Nada que possa levar o teclado ao modo de atualização de firmware ou a um reset de fábrica deve ser enviado.
- Remapeamentos de teste devem ser feitos apenas em perfil inativo (o 5), pois o teclado em teste costuma ser o mesmo usado para digitar.
- Claude não enxerga o teclado: efeitos de luz exigem confirmação visual do usuário.
- O Razer Synapse deve estar fechado e a Iluminação dinâmica do Windows, desligada.

## Documentação

| Arquivo | Assunto |
|---|---|
| [README.md](README.md) | Apresentação do projeto para o público geral e isenção de responsabilidade |
| [docs/PROTOCOLO.md](docs/PROTOCOLO.md) | Comandos e formatos do aparelho, o que foi validado e o que é hipótese, resultados dos testes de aceitação |
| [docs/ARQUITETURA.md](docs/ARQUITETURA.md) | Funcionamento interno de cada camada e decisões de projeto |
| [docs/BACKUP.md](docs/BACKUP.md) | Formato do JSON de backup e regras de importação |
| [docs/TESTES.md](docs/TESTES.md) | Testes seguros no aparelho, ferramentas, restauração e uso via computer-use |
| [docs/NOVO-MODELO.md](docs/NOVO-MODELO.md) | Levantamento dos valores e escrita da definição de outro teclado |
| [docs/USO.md](docs/USO.md) | Guia do usuário |
