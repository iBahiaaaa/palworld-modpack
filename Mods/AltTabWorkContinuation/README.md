# Trabalho Contínuo para Palworld

Mod de cliente para manter uma fabricação manual em andamento quando o jogador abre o inventário com `Tab` ou tira o foco do Palworld com `Alt+Tab`/tecla Windows.

Version: `0.4.1`

Build local alvo do Palworld: `24181527`

## Estado atual

O código está implementado e pronto para validação local. A versão ainda não foi instalada nem testada dentro do jogo.

Comportamento planejado:

- iniciar a fabricação continua sendo uma ação manual do jogador;
- `Tab` abre o inventário e inicia uma janela curta de observação; se o jogo encerrar a interação, é feita uma única restauração controlada;
- `Alt+Tab` e a tecla Windows não encerram a fabricação;
- interações recebidas como modo de segurar são convertidas para o modo de alternância que o próprio jogo já oferece;
- no modo de alternância, `F` continua iniciando e cancelando normalmente;
- morte, teleporte, troca de mapa e encerramentos que não sejam causados por `Tab` ou perda de foco seguem o comportamento normal do jogo;
- o mod não simula teclas, não altera saves e não muda a velocidade de fabricação.

## Como funciona

O código Lua só arma a proteção depois que o jogo confirma uma interação manual válida em modo de alternância. Ao receber o encerramento da interação:

- se houve perda recente de foco, o encerramento é neutralizado;
- quando o jogo autoriza a abertura de uma interface durante uma fabricação, são feitas no máximo 12 leituras, durante 300 ms, esperando o cancelamento automático acontecer;
- se a interação for encerrada nessa janela, somente uma chamada de restauração é permitida e seu resultado é verificado imediatamente;
- quando uma interação `F` chega com `isToggle=false`, o parâmetro é convertido para `true`, reproduzindo a opção nativa de alternância;
- nos demais casos, incluindo o cancelamento por `F`, o evento original não é modificado.

O helper nativo usa apenas `KERNEL32.dll` e `USER32.dll` para consultar foco e estado da tecla `Tab`. Ele não injeta entrada nem acessa a rede.

## Requisitos previstos

- Windows x64;
- Palworld Steam;
- [UE4SS específico para Palworld mantido por Okaetsu](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld);
- a opção **Pressionar uma vez para interações que exigem segurar** pode ficar ligada ou desligada; a versão `0.4.0-dev` força internamente o modo de alternância nas interações `F` suportadas;
- instalação em cada computador cujo jogador queira usar a função.

O servidor dedicado não precisa carregar este mod. Mesmo assim, o funcionamento conectado ao servidor de teste ainda precisa ser confirmado.

## Estrutura de instalação

Depois de compilado e validado, o pacote usa esta estrutura dentro da pasta do cliente Palworld:

```text
Pal\Binaries\Win64\ue4ss\Mods\AltTabWorkContinuation\
├── enabled.txt
└── Scripts\
    ├── main.lua
    ├── continuation_policy.lua
    └── AltTabWorkContinuationFocus.dll
```

Instalar, atualizar ou remover exige fechar completamente o Palworld, porque o helper nativo permanece carregado enquanto o processo do jogo estiver aberto.

## Validação manual obrigatória

1. Iniciar uma fabricação manual e abrir o inventário com `Tab`.
2. Confirmar que o inventário abriu e a fabricação continuou.
3. Fechar o inventário e confirmar que a fabricação ainda está ativa.
4. Repetir usando `Alt+Tab` e depois voltar ao jogo.
5. Pressionar `F` com o jogo em foco e confirmar que a fabricação foi cancelada.
6. Desativar temporariamente a opção de alternância, iniciar segurando `F`, soltar e confirmar que o trabalho continuou.
7. Pressionar `F` novamente e confirmar que o trabalho foi cancelado.
8. Testar morte, teleporte e desconexão para garantir que nenhuma interação fique presa.
9. Repetir conectado ao servidor dedicado de teste.

## Artefatos da versão 0.4.0-dev

| Arquivo | SHA-256 |
|---|---|
| `Native/pal_focus.c` | `3A39024D8AE46B5754C05402A7596EE53D44FC92A74BAEC2A0175B1F5A934607` |
| `Scripts/main.lua` | `4226081812030D14A43EB0ACF4B0124F022D42F6BA80B9B1F545CEFE051D2011` |
| `Scripts/continuation_policy.lua` | `3764C8C32F73CDE9CB18783276AA9BD173C8850B85B5DFC3C2EF67F0C6E8D708` |
| `Scripts/AltTabWorkContinuationFocus.dll` | `B3F2197986152C0ADE735CB1680CBB89E9BCBF3D46F7E170B3920D756EC758A1` |

## Créditos e licença

Este projeto deriva de [Palworld-AltTabWorkContinuation](https://github.com/Vercadi/Palworld-AltTabWorkContinuation), criado por Vercadi. O código-fonte original e estas modificações são distribuídos conforme a licença MIT mantida em `LICENSE`.
