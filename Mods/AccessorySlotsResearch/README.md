# Accessory Slots Research

Mod experimental cliente + servidor para adicionar até seis slots de acessório
ao Palworld.

A versão 0.7.0 disponibiliza dez slots em cinco colunas de duas linhas: quatro
slots vanilla e seis slots adicionais do mod. Os extras usam os índices de
armazenamento 9 a 14. Os novos slots copiam a
permissão do quarto slot de acessório nativo, permitindo que a validação do
servidor trate os destinos como acessórios.

Os seis botões adicionais herdam dos acessórios nativos as flags e os delegates
de clique, botão direito, arrastar, soltar, foco e levantamento de itens.
O painel de equipamentos recebe prioridade de interação sobre a visualização do
personagem, e o botão interno de cada slot adicional é vinculado explicitamente.
Assim que o contêiner real de 15 slots está pronto, a quantidade liberada é
retornada como dez desde a primeira consulta, antes de os botões aparecerem.
Cada botão é inicializado apenas uma vez para evitar ciclos de reconstrução da UI.
Ao receber `OnDrop` nos slots adicionais, o mod encaminha uma única vez para
`OnDropped_Internal`, reutilizando o fluxo do jogo antes do bloqueio do Blueprint.
Quando o objeto de drag fornece a origem, o controlador nativo da tela recebe
`SwapItemSlot` com os slots de origem e destino para efetuar a movimentação.
O controlador é localizado pela classe real, sem confundir o widget interno com
o nome do objeto pai presente no caminho completo.

O mesmo mod deve estar instalado no cliente e no servidor. O servidor cria os
slots antes das operações de inventário e o cliente associa os seis botões aos
mesmos IDs. A estrutura de save do jogo registra o número e o conteúdo dos slots
do contêiner; a persistência será validada primeiro no servidor de teste com um
acessório descartável e backup do mundo.

O arquivo `AccessorySlotsResearch-session.log` registra criação, carregamento,
movimentação e reconhecimento dos slots adicionais.

Depois de equipar, remover ou trocar um acessório, o cliente tenta a atualização
nativa do conteúdo interno e, para widgets criados dinamicamente, reaplica o
`Setup` no slot real. Assim, ícone e quantidade são redesenhados sem precisar
fechar e abrir novamente o inventário.

Como os widgets adicionais não recebem automaticamente todos os delegates do
Blueprint original, a atualização também chama diretamente `UpdateSlotEvent`,
`EmptySlotEvent` e `ValidSlotEvent` no filho visual exibido na tela.

As camadas `FocusBase` e `FocusFrame` são ocultadas apenas nos seis slots extras,
removendo a marcação azul durante o arraste sem desativar a área de soltar.
Como o Blueprint pode reativar essas camadas ao receber hover ou foco, a
ocultação é reaplicada após o evento e novamente depois do início da animação.

A quantidade solicitada fica em `Scripts/config.lua`. O módulo independente
`slot_limits.lua` aplica uma trava rígida de dez slots totais em todas as
camadas: interface, validação, quantidade desbloqueada e contêiner persistido.
Mesmo que `requestedAccessorySlots` receba um valor maior, somente dez são
expostos e aceitos pelo mod.
