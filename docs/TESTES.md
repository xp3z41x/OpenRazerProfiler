# Testes no aparelho

O projeto não possui testes unitários: toda a validação é feita no teclado real, por três meios independentes:
1. o próprio aplicativo, que relê o estado e o compara com o esperado após cada gravação;
2. `testes\TesteAparelho`, um programa de console que usa o mesmo código de `src\Protocolo` e `src\Aparelhos`;
3. os scripts Python de `investigacao\`, que usam a biblioteca hidapi e não dependem do código C#, permitindo uma conferência externa.

## Regras de segurança

Estas regras fazem parte dos requisitos do projeto desde o início e se aplicam a qualquer teste:
- **Um backup completo deve ser feito antes de qualquer gravação na memória permanente** e, ao final dos testes, o teclado deve voltar ao estado do backup, salvo quando se decidir manter alguma alteração.
- Comandos de **leitura** (bit `0x80`) podem ser varridos livremente. Comandos de **escrita** devem ser enviados um de cada vez, a partir de uma hipótese, e sempre seguidos da leitura correspondente.
- Nenhum comando que possa colocar o teclado em modo de atualização de firmware ou provocar um reset de fábrica deve ser enviado.
- **Remapeamentos devem ser feitos apenas em perfil inativo**, ou desfeitos logo em seguida, pois o teclado em teste costuma ser o mesmo usado para digitar. O perfil 5 é reservado para testes.
- Quando os testes são conduzidos por um assistente de IA, como o Claude Code, ele não enxerga o teclado: **efeitos de luz exigem confirmação visual de uma pessoa**, com uma pergunta por estado do teclado.
- Testes visuais e funcionais exigem uma pessoa diante do teclado, o que não ocorre quando o computador é acessado remotamente (por exemplo, por RDP).
- O Razer Synapse deve permanecer fechado e a Iluminação dinâmica do Windows, desligada, durante os testes.

## Backups de referência

A pasta `backup\` não é versionada, pois os arquivos contêm o número de série do aparelho. Antes do primeiro teste, devem ser criados nela dois backups do estado original do teclado:

| Arquivo | Formato | Geração | Uso |
|---|---|---|---|
| `backup\backup-inicial.json` | investigação | `python backup.py ..\backup\backup-inicial.json`, a partir de `investigacao\` | referência para `comparar.py` |
| `backup\backup-original.json` | aplicativo | *Backup › Exportar backup…* ou `OpenRazerProfiler.exe --exportar backup\backup-original.json` | restauração pela interface ou por `TesteAparelho aplicar` |

Os dois formatos estão descritos em [BACKUP.md](BACKUP.md).

## Ferramentas

**TesteAparelho.** É compilado com `dotnet build testes\TesteAparelho\TesteAparelho.csproj -c Release` e executado com `testes\TesteAparelho\bin\Release\TesteAparelho.exe <comando>`:

| Comando | Função | Grava no teclado? |
|---|---|---|
| `validar` | Verifica definições coerentes e incoerentes, tabelas de polling, leitura como modelo sem polling, recusa de família incorreta e bloqueio de códigos inválidos antes do envio. Ao final, confere que o teclado não foi alterado. | Não |
| `ler [saida.json]` | Lê todo o estado (cerca de 0,5 s) e exibe um resumo. Com arquivo, salva o backup e testa a ida e volta pelo JSON. | Não |
| `comparar <backup.json>` | Lista as diferenças entre o teclado e um backup no formato do aplicativo. | Não |
| `aplicar <backup.json>` | Executa `DeviceSync.Save`: grava as diferenças, relê e confere. | **Sim** |
| `ativar <slot>` | Troca o perfil ativo e mostra o que divergir na primeira releitura. | **Sim** |

**Scripts Python.** Requerem Python 3 e `pip install hidapi` e devem ser executados a partir de `investigacao\`, pois importam `rz.py` do diretório atual:

| Script | Função |
|---|---|
| `rz.py` | Cliente mínimo do protocolo: `Razer().get(cls, cmd, args, size)`, `xfer` e `must`. Com `verbose=True`, imprime os pacotes. |
| `backup.py <saida.json>` | Backup completo no formato de investigação (o mesmo de `backup\backup-inicial.json`). |
| `comparar.py <backup.json>` | Compara o teclado com um backup no formato de investigação, incluindo o polling de cada perfil. O resultado esperado é `IGUAL AO BACKUP`. |
| `luz.py` | Mostra a iluminação gravada no perfil ativo e a iluminação ao vivo. |
| `varredura_leitura.py <saida.json>` | Varre todos os comandos GET das classes `0x00` a `0x10`. O resultado da varredura do Huntsman V2 está em `varredura_leitura.json`. |
| `estado_perfil.py` | Funções `estado` e `diff`, para comparar um perfil antes e depois de um comando. |
| `teclas_hook.py <saida.txt> <segundos>` | Registra, por tempo limitado, apenas as teclas do teste funcional; as demais são registradas como "outra tecla". |

## Procedimento padrão

1. Conferir o ponto de partida: `python comparar.py ..\backup\backup-inicial.json` deve informar `IGUAL AO BACKUP`. Se o estado de referência tiver mudado intencionalmente, fazer um novo backup antes de qualquer gravação.
2. Testar no perfil 5. Para gravar cenários sem a interface, editar uma cópia de um backup no formato do aplicativo e aplicá-la com `TesteAparelho aplicar`.
3. Conferir externamente com `comparar.py`, que deve listar apenas as diferenças esperadas.
4. Restaurar o estado original pela interface (*Backup › Importar backup…*, com o arquivo `backup\backup-original.json`, seguido de *Gravar no teclado*) ou, a partir de `testes\TesteAparelho\`, com `bin\Release\TesteAparelho.exe aplicar ..\..\backup\backup-original.json`.
5. Conferir novamente: `python comparar.py ..\backup\backup-inicial.json` deve informar `IGUAL AO BACKUP`.

## Confirmação visual da iluminação

- O efeito deve ser gravado e conferido com `luz.py` antes da solicitação de confirmação visual. Cada pergunta deve tratar de um único estado, com opções que distingam o resultado correto do incorreto (por exemplo, o sentido da onda).
- Velocidades muito baixas do wave (`0x01`) criam a ilusão de movimento no sentido contrário. Para conferir a direção, recomenda-se usar `0x10` ou mais.
- O reactive aparece apenas durante a digitação. Duração e velocidade são conferidas comparando os dois extremos em sequência ("apaga mais rápido ou mais devagar que antes?").
- No starlight, cada estrela tem duração diferente; esse comportamento é normal.

## Teste funcional de teclas

1. Gravar os remapeamentos no perfil 5 e ativá-lo (por exemplo, com `TesteAparelho aplicar` e um backup com `perfil_ativo` igual a 5).
2. Executar `python teclas_hook.py <saida.txt> 180` em segundo plano.
3. Solicitar à pessoa diante do teclado que pressione as teclas uma de cada vez, em ordem conhecida.
4. Ler o arquivo: cada tecla remapeada deve gerar o novo código, e uma tecla desativada não deve gerar nenhum evento.
5. Restaurar o backup e, em seguida, apagar o registro.

## Testes da interface por computer-use

Orientações para testar a interface com o recurso de computer-use do Claude:
- O executável é portátil e não aparece no Menu Iniciar; o acesso deve ser solicitado pelo caminho completo, por exemplo: `request_access(["<caminho do repositório>\dist\OpenRazerProfiler.exe"])`.
- Com a janela inativa, o primeiro clique apenas a ativa.
- Se a área de trabalho mudar de tamanho (como ocorre no acesso por RDP), os cliques podem não ser concluídos. A navegação pelo teclado continua disponível:
  - Espaço aciona botões;
  - as setas mudam a opção em controles segmentados e em listas suspensas (Espaço, Enter ou Alt+↓ abrem a lista);
  - no campo de tecla, digitar filtra a lista, as setas escolhem um item e Enter confirma;
  - Home e End atuam nas barras deslizantes;
  - Shift+F10 na lista de perfis abre o menu do perfil.
- No seletor de cor do Windows, a escolha deve ser feita pela grade de cores básicas (2ª linha: vermelho, amarelo…), sem digitação nos campos.
- A posição das opções do editor de teclas varia conforme o texto ("Padrão (…)"); recomenda-se ampliar a captura antes de clicar.
- O desenho do teclado não indica o que cada tecla faz, apenas muda a cor. Para conferir um remapeamento, deve-se clicar na tecla e ler o editor.
- *Gravar no teclado* termina com "Gravado e conferido às HH:MM" no rodapé. O rodapé não lista pendências: elas são indicadas pelo ponto verde nos perfis e pelo botão *Gravar no teclado* habilitado. O número de gravações realizadas pelo `Save` é informado pelo `TesteAparelho aplicar`.

Os resultados dos testes de aceitação de 22/10/2026 estão no fim de [PROTOCOLO.md](PROTOCOLO.md).
