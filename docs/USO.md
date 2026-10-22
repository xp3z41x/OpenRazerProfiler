# Guia de uso

O `OpenRazerProfiler.exe` é um arquivo único, sem instalador e sem processos em segundo plano. O uso consiste em abrir o aplicativo, ler o teclado, fazer as alterações, gravá-las e fechar. Todas as configurações ficam armazenadas no próprio teclado. O aplicativo é executado como usuário comum, sem privilégios de administrador.

## Antes de usar

O Razer Synapse deve ser encerrado e a Iluminação dinâmica do Windows deve ser desativada (Configurações › Personalização › Iluminação dinâmica). Ambos disputam o controle do teclado, e o aplicativo exibe um aviso quando detecta qualquer um deles.

## Fluxo básico

1. Abrir o aplicativo. Ao iniciar, ele lê todo o conteúdo do teclado, o que leva cerca de 0,5 s.
2. Selecionar um perfil na lista à esquerda e fazer as alterações nas abas **Iluminação** e **Teclas**.
3. Clicar em **Gravar no teclado** (Ctrl+S). Apenas o que foi alterado é enviado; em seguida, o aplicativo relê todo o estado e confere o resultado. O rodapé informa "Gravado e conferido" ou indica o que não correspondeu ao esperado.

Nenhuma alteração é enviada ao teclado antes de **Gravar no teclado**. Enquanto houver alterações não gravadas, o botão **Gravar no teclado** permanece habilitado, a opção *Descartar alterações* (que retorna ao estado gravado) fica disponível e os perfis alterados exibem um ponto verde ao lado do nome. **Ler do teclado** (F5) relê todo o estado.

## Perfis

O teclado possui 5 slots. As ações **Ativar**, **Renomear…**, **Criar…** e **Apagar** atuam sobre o slot selecionado e estão disponíveis tanto nos botões quanto no menu de contexto do perfil, aberto com o clique direito, com Shift+F10 ou com a tecla Menu. O perfil ativo, identificado pelo número preenchido e pela indicação "Ativo", não pode ser apagado; para isso, é preciso ativar outro perfil antes.

Um perfil recém-criado recebe o padrão do firmware: efeito spectrum, brilho máximo e teclas de fábrica.

## Iluminação

Estão disponíveis apenas os efeitos do próprio firmware:

| Efeito | Opções |
|---|---|
| Desligado | — |
| Estático | cor |
| Breathing | uma ou duas cores, ou cores aleatórias |
| Spectrum | — |
| Wave | direção e velocidade |
| Reactive | cor e duração do brilho |
| Starlight | uma ou duas cores, ou cores aleatórias, e duração |

O brilho também pode ser ajustado. Um efeito gravado por outro programa e que o aplicativo não edita (como um efeito personalizado do Synapse) aparece em uma etiqueta própria, já selecionada; a escolha de outro efeito o substitui.

No perfil ativo, as alterações aparecem no teclado imediatamente, como prévia, mas são salvas apenas ao gravar. A iluminação de um perfil inativo passa a ser exibida quando esse perfil é ativado.

## Teclas

O desenho do teclado indica o estado de cada tecla apenas pela cor: verde para tecla alterada, cinza para tecla desativada e branco para a função de fábrica. Ao clicar em uma tecla, o editor exibe a ação atual e permite escolher outra:
- **Padrão**: a função de fábrica;
- **Outra tecla ou combinação**: Ctrl, Shift, Alt e Win combinados a uma tecla. No campo, a busca é feita pela digitação de parte do nome (como "ctrl" ou "F13"), e a escolha é confirmada com Enter ou com um clique na lista;
- **Mídia ou sistema**: reproduzir/pausar, troca de faixa, volume, mudo, calculadora, navegador, e-mail, explorador de arquivos ou suspensão do computador;
- **Desativada**: a tecla deixa de funcionar.

A tecla Fn não pode ser remapeada: ao selecioná-la, as opções aparecem desabilitadas. A opção *Restaurar todas as teclas deste perfil* devolve o perfil inteiro ao padrão e fica disponível apenas quando há alguma tecla alterada ou desativada.

## Taxa de polling

O controle fica no rodapé e vai de 125 a 8000 Hz. O Huntsman V2 armazena a taxa em cada perfil, e o aplicativo grava o valor escolhido em todos eles; a indicação "Varia por perfil" aparece quando os perfis têm taxas diferentes. Acima de 1000 Hz, o teclado precisa estar conectado diretamente a uma porta USB da placa-mãe, e não a um hub lento ou a um KVM.

## Backup

- **Backup ▾ → Exportar backup…** salva todos os perfis em um arquivo JSON, com os bytes exatos lidos do teclado. Alterações pendentes não são incluídas.
- **Backup ▾ → Importar backup…** carrega um arquivo como alterações pendentes, que são aplicadas com **Gravar no teclado**. Apenas backups do mesmo modelo são aceitos.
- A exportação também pode ser feita sem abrir a janela: `OpenRazerProfiler.exe --exportar backup.json`.

O formato do arquivo está descrito em [BACKUP.md](BACKUP.md).

## Avisos

| Aviso | Ação |
|---|---|
| Razer Synapse em execução | Encerrar o Synapse e clicar em *Verificar de novo*. |
| Iluminação dinâmica do Windows ligada | Desativá-la nas Configurações do Windows. |
| Teclado em modo driver | Clicar em *Voltar ao modo normal*. Essa situação ocorre quando um programa assume o controle da iluminação e não o devolve. |
