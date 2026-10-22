# Arquitetura do código

O código é dividido em três partes, compiladas em um único assembly, com dependência em um único sentido:

| Pasta | Namespace | Responsabilidade | Depende de |
|---|---|---|---|
| `src\Protocolo` | `OpenRazerProfiler.Protocolo` | Transporte HID, pacotes, comandos, estado, sincronização e backup | nenhuma outra parte (apenas .NET) |
| `src\Aparelhos` | `OpenRazerProfiler.Aparelhos` | Definições de modelo, catálogo e conexão, nomes de teclas | Protocolo |
| `src\Interface` | `OpenRazerProfiler.Interface` | Interface gráfica em WinForms | Protocolo e Aparelhos |

O projeto `testes\TesteAparelho` compila `Protocolo` e `Aparelhos` como aplicativo de console, sem `Interface`. Por esse motivo, essas duas pastas não podem usar WinForms nem `System.Drawing`.

## Protocolo

**Transporte.** `HidEnumerator` usa a SetupAPI para listar as interfaces do VID/PID; o número `MI_xx` de cada interface é extraído do caminho do dispositivo. `HidFeatureChannel` abre esse caminho; se a abertura com leitura e escrita falhar (o Windows abre teclados em modo exclusivo), o caminho é reaberto sem direitos de acesso, e os feature reports continuam funcionando. `RazerTransport.Send`:
- monta o pacote (`RazerPacket.Request`: 90 bytes mais o report ID 0, com CRC igual ao XOR dos bytes 2 a 87);
- executa `SetFeature`, aguarda 2 ms e repete `GetFeature` enquanto o status for `00` ou `01`;
- descarta respostas de outro comando e respostas cujo eco dos primeiros `echo` argumentos não corresponda ao pedido;
- faz 3 tentativas de 1,5 s cada, configuráveis em `ResponseTimeoutMs` e `Attempts`.

`Execute` lança `RazerException` quando o status não indica sucesso. As pausas de 1–2 ms dependem de `timeBeginPeriod(1)`, ativado em `Program.Main` e no `TesteAparelho`.

**Comandos.** `RazerKeyboard` tem um método por comando, já com o tamanho de dados e o eco de cada um. O transaction id, o LED e a família de polling são recebidos pelo construtor, a partir da definição do modelo. Detalhes não evidentes:
- `ReadKeymap` lê 9 teclas por pedido e identifica o fim do mapa com a leitura individual `02 8D`, porque a resposta após a última tecla é idêntica à de uma tecla desativada;
- `GetPollingCode` e `SetPollingCode` escolhem os comandos de acordo com a `PollingKind`;
- `SetPollingCode` recusa códigos fora da tabela antes do envio, pois o firmware aceitaria qualquer valor.

**Dados.**

| Tipo | O que representa |
|---|---|
| `KeyBinding` | Entrada `[tipo, tamanho, dados]` do mapa de teclas. `Equals` compara byte a byte; `Normalized()` e `SameAction()` tratam o modificador como usage `E0`–`E7` ou como bit da máscara, indistintamente. |
| `LightingEffect` | `[efeito, p1, p2, n, RGB…]`; `ToArgs(onde, led)` monta os argumentos de `0F 02` (tamanho 6 + 3n). |
| `ProfileMetadata` | Decodifica o nome nos formatos de fábrica (UTF‑16BE) e do Synapse (250 B). `EncodeName` mantém o formato existente; perfis novos usam UTF‑16BE. |
| `PollingKind` / `PollingCodes` | Família de comandos de polling e respectiva tabela de códigos. |

**Estado.** `DeviceState` armazena o que o aplicativo lê e grava: informações do aparelho, polling, perfil ativo, capacidade de slots e `Profiles` (apenas os slots existentes). Cada `ProfileState` contém metadados, efeito, brilho, polling (somente na família por perfil) e as camadas `Keys` (base) e `FnKeys`. `Clone()` faz cópia profunda dos dicionários; `KeyBinding` e `LightingEffect` são imutáveis.

**Sincronização** (`DeviceSync`):
- `ReadAll` lê as informações do aparelho, a lista de perfis (`05 81`) e, para cada perfil, metadados, efeito, brilho, polling e as duas camadas de teclas. A leitura completa leva cerca de 0,5 s.
- `StateDiff.Compare(de, para)` faz comparação exata. A criação ou a exclusão de um perfil conta como um único item; na família clássica, o polling aparece como item com `Slot = 0`.
- `Apply` grava na seguinte ordem:
  1. polling da família clássica;
  2. cada perfil desejado: se o perfil não existe, `05 02` o cria e a referência de comparação passa a ser `NewProfileTemplate`, com regravação forçada de metadados e polling, já que o `05 02` não reinicia esses dois itens;
  3. perfil ativo;
  4. exclusão dos perfis remanescentes (nunca o perfil ativo desejado).
- `Save` executa `Apply`, `ReadAll` e `Compare(relido, desejado)`. Se algo não conferir, executa `Apply` novamente a partir do estado relido e relê. O `SaveResult` contém o estado relido, as divergências e o número de gravações.

**Backup.** `BackupFile` serializa e lê o JSON com o leitor e o escritor próprios de `Json.cs` (sem dependências externas; `JsonObject` preserva a ordem dos campos). O formato está descrito em [BACKUP.md](BACKUP.md).

## Aparelhos

- `DeviceDefinition`: tudo o que varia entre modelos, além de `Validate()`, `OffersPollingCode()` e `NewProfileTemplate()`. Os campos e a forma de obtê-los estão em [NOVO-MODELO.md](NOVO-MODELO.md).
- `HuntsmanV2.Definition`: mapa físico ANSI construído com `LayoutBuilder`, mapas de fábrica em texto (`KeyTable.Parse`) gerados a partir da leitura do perfil 1 e limites conferidos no aparelho.
- `DeviceCatalog.Connect()`:
  - executa `CheckDefinitions()`;
  - para cada definição, lista as interfaces HID do VID/PID e testa o comando `00 81` com timeout curto (250 ms, 1 tentativa), começando pela `ControlInterface`;
  - retorna a definição, o `RazerKeyboard` pronto para uso e a interface escolhida.
- `HidUsages`: nomes em português das usages de teclado (versão curta para o desenho e longa para as listas), a lista de ações de mídia e de sistema e `Describe()`, que converte uma ação em texto.

## Interface

- `Program.Main`: modo `--exportar arquivo.json` (sem janela; usa `AttachConsole` para exibir mensagens) e, nos demais casos, abre a `MainForm`.
- `Session`:
  - mantém `Device` (o último estado lido ou conferido) e `Working` (o estado desejado pelo usuário); `Pending = Compare(Device, Working)`;
  - `Read` e `Save` desconectam o aparelho em caso de erro, e a leitura seguinte reconecta;
  - `Preview` grava efeito e brilho somente na RAM (`onde = 0`), e `EndPreview` devolve à RAM o que está gravado no perfil ativo;
  - `LoadBackup` aplica as regras de importação.
- `MainForm`:
  - toda edição altera apenas o `Working` e permanece pendente até *Gravar no teclado*;
  - as operações no teclado são executadas em `Task.Run` por meio de `RunDeviceTask`, com a interface desabilitada enquanto `busy` estiver ativo;
  - a prévia de iluminação passa por um `Timer` de 80 ms e se aplica apenas ao perfil que está ativo **no aparelho**;
  - ao fechar com pendências, pergunta se deve gravá-las; ao fechar, sempre chama `EndPreview`;
  - exibe os avisos de Synapse em execução, Iluminação dinâmica ligada e modo driver;
  - não descreve em texto o que está pendente: `AfterEdit` limpa o rodapé, e o estado aparece no botão *Gravar no teclado*, em *Descartar alterações* e no ponto verde dos perfis. O rodapé é reservado a progresso, resultado de operações e erros;
  - a lista de perfis tem menu de contexto (Criar, Renomear, Ativar, Apagar) com as mesmas regras dos botões (`SlotActions`).
- `LightingEditor`: monta o efeito a partir dos controles, preservando os valores que o usuário não alterou (velocidade, brilho, p2 do breathing e cores armazenadas). Sem essa preservação, abrir um perfil e trocar de aba gravaria valores arredondados. A barra do wave usa `WaveSpeeds` da definição; reactive e starlight usam os limites de duração. Um efeito gravado que não consta da lista (por exemplo, um efeito personalizado do Synapse) aparece em uma etiqueta adicional, já marcada; escolher outro efeito o substitui, e voltar a essa etiqueta restaura o efeito original.
- `KeyEditor`: a opção "Padrão" usa `SameAction`, mantendo a forma gravada quando a ação já é a de fábrica; modificadores do lado direito vindos do teclado são preservados. O campo de tecla é um `SearchPicker`, que filtra a lista durante a digitação e confirma a escolha apenas com Enter, com um clique ou ao sair do campo. Sem tecla escolhida, o campo fica vazio; na tecla Fn, as opções aparecem desabilitadas.
- `KeyboardView`: desenha `Definition.Layout` em unidades de tecla escaladas para o controle, a partir do canto superior esquerdo (alinhado com a legenda e com o editor). Cada tecla exibe apenas o próprio nome; o estado vem de `MainForm.DescribeKey` e é indicado somente pela cor: padrão, alterada ou desativada (a Fn aparece como padrão). `LegendItem` desenha a legenda com amostras em formato de tecla.
- Controles desenhados pelo próprio aplicativo substituem os controles nativos do Windows. Todos têm os estados de hover, pressionado, foco (visível apenas na navegação por teclado) e desabilitado, e calculam suas medidas a partir de `DeviceDpi`:
  - `Controls.cs`: `FlatButton` (primário, secundário ou apenas texto; com `DropDownMenu`, abre um menu e exibe uma seta), `TextField` (caixa de texto com a moldura do aplicativo), `ChipRadio` (as etiquetas de efeito), `KeycapToggle` (modificadores em formato de tecla), `SegmentedControl` (escolha entre poucas opções) e `Slider`;
  - `Menus.cs`: `ModernMenu` (`ToolStripDropDown` com itens `MenuEntry` e `MenuDivider` que se medem e se desenham), `ContextMenus` (associa um `ModernMenu` ao clique direito, a Shift+F10 e à tecla Menu), `DropDownButton` (lista suspensa que abre um `ModernMenu` com ✓ no item atual), `EditMenu` (menu de contexto das caixas de texto) e `Dwm` (cantos do Windows 11);
  - `SearchPicker.cs`: o campo com busca e a janela da lista, que não recebe ativação (o foco permanece no texto) e se fecha com um clique externo, detectado por um `IMessageFilter`;
  - `Theme.cs`: `TextLabel` (o rótulo criado por `Theme.Label`, sem o recuo interno que o Windows aplica nas laterais do texto), `Card` (cartão arredondado; o Padding de 4 px mantém os filhos dentro da curva) e `ColorSwatch` (abre o seletor de cor do Windows).
- Demais componentes: `Theme` (cores, fontes, medidas e utilitários de desenho), `ProfileList` (lista desenhada, com seleção em formato de pílula), `TabStrip` (o texto da primeira aba começa na borda do cartão), `NameDialog` e `SystemChecks` (processos da Razer e chave de registro da Iluminação dinâmica).

## Decisões e armadilhas

- **Toda gravação é permanente.** Não há comando de "salvar": cada `0F 02`, `02 0D` e `05 08` vai diretamente para a memória do teclado. Daí a comparação exata e o cuidado de não gerar alterações que o usuário não fez.
- **Exato × normalizado.** Diferenças, gravação, verificação e backup usam igualdade byte a byte; somente a interface usa `SameAction`, para indicar se uma tecla está "no padrão".
- **Polling.** No Huntsman V2, a taxa fica armazenada em cada perfil, mas a interface exibe um único controle e grava o valor em todos, para que a taxa não mude ao trocar de perfil.
- **Perfil ativo.** Gravar iluminação ou polling no slot ativo atualiza o valor ao vivo. Ativar outro perfil carrega a iluminação, as teclas e o polling desse perfil.
- **DPI.** O `app.manifest` declara apenas reconhecimento de DPI do sistema, sem `app.config`, para manter o aplicativo em um único arquivo. A janela usa `AutoScaleMode.Dpi` sobre medidas projetadas para 96 DPI; os controles desenhados à mão calculam suas medidas a partir de `DeviceDpi` ou do próprio tamanho.
- **Controles próprios.** Os controles que herdam de `Button`, `CheckBox` e `RadioButton` apenas substituem o desenho: continuam valendo `DialogResult`, `AcceptButton`, a tecla Espaço, as setas entre botões de opção e a acessibilidade nativa. Os que herdam de `Control` (`SegmentedControl`, `Slider` e `DropDownButton`) implementam teclado, foco e `AccessibleObject` por conta própria. Assim como nos controles nativos, `SelectedIndexChanged` e `ValueChanged` também disparam em alterações feitas pelo código; por isso, os editores usam a flag `loading`.
- **Cantos do Windows 11.** Os menus e a lista de busca solicitam `DWMWA_WINDOW_CORNER_PREFERENCE` ao DWM, que passa a desenhar cantos arredondados, borda e sombra. No Windows 10, o atributo não existe, a chamada falha e o renderizador desenha uma borda reta.
- **Menus fora do `ContextMenuStrip`.** O menu nativo (`ToolStripDropDownMenu`) posiciona o texto do `ToolStripMenuItem` por conta própria (no alto do item), altera o `Padding` durante o layout e reserva, à direita de todos os itens, o espaço da seta de submenu. Por isso, o `ModernMenu` herda de `ToolStripDropDown`, e os itens medem e desenham todo o seu conteúdo. A navegação por teclado (setas, Enter, Esc) e o fechamento ao clicar fora continuam a cargo do `ToolStripDropDown`. Propriedades que exigem um `ContextMenuStrip` recebem uma instância vazia, que apenas repassa a solicitação (`ContextMenus`).
- **Formas e texto na mesma grade.** O texto desenhado pelo `TextRenderer` (GDI, com ClearType) cai em pixels inteiros. Já o GDI+ suavizado, no `PixelOffsetMode` padrão, trata a coordenada inteira como o centro do pixel: as bordas de 1 px saíam borradas em dois pixels de meio-tom, e as formas ficavam deslocadas meio pixel em relação ao texto. Todo desenho suavizado passa por `Theme.Smooth` (`PixelOffsetMode.Half`), e as bordas de 1 px ficam em coordenadas x,5.
- **Texto centralizado pela tinta.** A caixa que o Windows mede para um texto inclui as margens laterais de cada letra e o espaço reservado a acentos e descendentes; centralizar por ela deixava números e nomes até 1,5 px fora do centro. O texto de botões, etiquetas, teclas modificadoras e opções passa por `Theme.DrawCenteredText`, que centraliza na horizontal pela tinta e na vertical pela altura da linha, para que a linha de base coincida com a dos campos vizinhos. A tinta é obtida por `Theme.InkOf`, que desenha o texto uma vez em uma imagem à parte e considera cada borda no ponto em que a cobertura cruza a metade. O nome das teclas do `KeyboardView` também é centralizado na vertical, pela faixa das letras maiúsculas. O número do perfil, pequeno e dentro de um círculo, é desenhado como contorno (`Theme.DrawCenteredGlyphs`) e fica centralizado com precisão de fração de pixel. Nos demais casos, resta um desvio de até meio pixel, porque o GDI desenha texto apenas em pixels inteiros.
- **Sem frases explicativas.** Conforme o [CLAUDE.md](../CLAUDE.md), a interface não descreve seu funcionamento nem o que está pendente; essas informações ficam na documentação.
- **Linhas ocultas antes da criação do handle.** Um controle oculto desde o início cria o handle apenas quando é exibido, e nesse momento o WinForms pode reordenar a coleção de filhos, o que altera a ordem das linhas em um `TableLayoutPanel` ou `FlowLayoutPanel` sem posições fixas. O `LightingEditor` é exibido com todas as linhas visíveis e somente depois recebe o primeiro `Bind`; essa ordem deve ser mantida, ou as células devem ser fixadas.
- **C# moderno no .NET Framework 4.8** (`LangVersion latest`): apenas recursos de sintaxe. Os que dependem de tipos ou do runtime não estão disponíveis: `record`/`init`, membros padrão em interfaces e `Index`/`Range`.
