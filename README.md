# Palworld Modpack

Modpack de cliente para o servidor de amigos do Bahiaaaa. O launcher consulta as
[GitHub Releases](https://github.com/iBahiaaaa/palworld-modpack/releases), atualiza
o modpack e o próprio launcher e abre o Palworld pela Steam.

## Para jogadores

1. Baixe `Palworld-Modpack-Launcher.exe` na Release mais recente.
2. Feche o Palworld.
3. Abra o launcher e confirme a pasta detectada.
4. Aguarde a atualização automática.
5. Clique em **Jogar com mods**.

O launcher aceita a pasta da Steam, uma biblioteca Steam, a pasta do Palworld ou
a pasta `Win64`. Se o GitHub estiver indisponível, ele permite abrir o jogo com a
versão que já está instalada.

O carregador dos mods fica desativado por padrão. Abrir o Palworld diretamente
pela Steam inicia o jogo vanilla. O botão **Jogar com mods** ativa o UE4SS apenas
durante aquela sessão, mantém o launcher minimizado e volta ao modo vanilla
quando o jogo fecha. O botão **Jogar vanilla** também está disponível no launcher.

O botão **Remover mods** cria um backup e apaga somente os arquivos gerenciados
pelo modpack. Outros diretórios de mods são preservados. Para reinstalar, basta
usar **Jogar com mods** novamente.

O botão **Instalar launcher** copia o aplicativo para o perfil local do Windows
e cria atalhos na Área de Trabalho e no Menu Iniciar, sem pedir acesso de
administrador. A autoatualização passa a substituir essa cópia instalada.

A versão 1.1.0 precisa ser baixada manualmente uma vez. Depois dela, novas versões
do próprio launcher são baixadas, verificadas por SHA-256 e aplicadas automaticamente.
O executável do launcher pode ser atualizado mesmo com o Palworld aberto. Apenas
as atualizações dos arquivos do modpack aguardam o fechamento do jogo.

## Mods incluídos

### Hover Transfer

Abra um armazenamento, mantenha `H` pressionado e passe o cursor sobre os itens.
A transferência usa as funções internas de inventário do Palworld; não envia
cliques nem teclas simuladas ao Windows.

### AltTab Work Continuation

Mantém a fabricação manual ativa ao usar `Alt+Tab`. Também tenta preservar o
trabalho ao abrir o inventário e oferece o modo de interação contínua do `F`.

### Accessory Slots Research

Expande os acessórios para 10 slots totais: quatro vanilla e seis adicionais.
Os slots extras equipam, atualizam o visual e permanecem salvos no servidor.
Todos os jogadores devem manter a mesma versão instalada pelo launcher.

## Segurança da atualização

- O repositório de atualização é fixo em `iBahiaaaa/palworld-modpack`.
- O pacote é baixado por HTTPS diretamente do GitHub.
- O SHA-256 é validado antes de qualquer arquivo ser alterado.
- O executável novo do launcher também é validado antes da substituição.
- Caminhos inseguros dentro do ZIP são recusados.
- Arquivos substituídos recebem backup.
- Somente arquivos listados pelo modpack são gerenciados; outros mods são preservados.

## Estrutura

- `Launcher/`: launcher gráfico e atualizador.
- `Release/`: criação do pacote e publicação das Releases.
- `Native/`: auxiliar nativo do Hover Transfer.
- `Mods/AltTabWorkContinuation/`: código e auxiliar nativo do mod de fabricação.
- `Mods/AccessorySlotsResearch/`: expansão dos slots de acessório.
- `Scripts/`: código Lua, configuração, compilação e validação do mod.
- `Installer/`: instalador antigo, mantido como alternativa.

## Gerar e testar

```powershell
.\Release\build-modpack-package.ps1 -Version 0.6.3
.\Launcher\build-launcher.ps1 -Version 1.4.0
.\Launcher\test-launcher.ps1 -ModpackVersion 0.6.3
.\Launcher\test-modpack-update.ps1
.\Launcher\test-self-update.ps1 -LauncherVersion 1.4.0
```

## Publicar uma atualização

Atualize o número da versão e execute:

```powershell
.\Release\publish-release.ps1 -ModpackVersion 0.6.3 -LauncherVersion 1.4.0
```

O script monta o pacote, compila o launcher, executa o teste completo e cria a
Release somente se todas as etapas forem concluídas.

Se uma publicação falhar somente durante o upload, repita sem recompilar os
arquivos que já passaram nos testes:

```powershell
.\Release\publish-release.ps1 -ModpackVersion 0.6.3 -LauncherVersion 1.4.0 -SkipBuild
```

## Licenças

O código deste projeto usa a licença MIT. O pacote distribui o UE4SS com a cópia
da licença original dentro de `ue4ss/LICENSE`.
