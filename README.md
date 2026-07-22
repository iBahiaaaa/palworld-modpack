# Palworld Modpack

Modpack de cliente para o servidor de amigos do Bahiaaaa. O launcher consulta as
[GitHub Releases](https://github.com/iBahiaaaa/palworld-modpack/releases), instala
atualizações verificadas e abre o Palworld pela Steam.

## Para jogadores

1. Baixe `Palworld-Modpack-Launcher.exe` na Release mais recente.
2. Feche o Palworld.
3. Abra o launcher e confirme a pasta detectada.
4. Aguarde a atualização automática.
5. Clique em **Jogar Palworld**.

O launcher aceita a pasta da Steam, uma biblioteca Steam, a pasta do Palworld ou
a pasta `Win64`. Se o GitHub estiver indisponível, ele permite abrir o jogo com a
versão que já está instalada.

## Mods incluídos

### Hover Transfer

Abra um armazenamento, mantenha `H` pressionado e passe o cursor sobre os itens.
A transferência usa as funções internas de inventário do Palworld; não envia
cliques nem teclas simuladas ao Windows.

### AltTab Work Continuation

Mantém a fabricação manual ativa ao usar `Alt+Tab`. Também tenta preservar o
trabalho ao abrir o inventário e oferece o modo de interação contínua do `F`.

## Segurança da atualização

- O repositório de atualização é fixo em `iBahiaaaa/palworld-modpack`.
- O pacote é baixado por HTTPS diretamente do GitHub.
- O SHA-256 é validado antes de qualquer arquivo ser alterado.
- Caminhos inseguros dentro do ZIP são recusados.
- Arquivos substituídos recebem backup.
- Somente arquivos listados pelo modpack são gerenciados; outros mods são preservados.

## Estrutura

- `Launcher/`: launcher gráfico e atualizador.
- `Release/`: criação do pacote e publicação das Releases.
- `Native/`: auxiliar nativo do Hover Transfer.
- `Mods/AltTabWorkContinuation/`: código e auxiliar nativo do mod de fabricação.
- `Scripts/`: código Lua, configuração, compilação e validação do mod.
- `Installer/`: instalador antigo, mantido como alternativa.

## Gerar e testar

```powershell
.\Release\build-modpack-package.ps1 -Version 0.4.0
.\Launcher\build-launcher.ps1 -Version 1.0.0
.\Launcher\test-launcher.ps1 -ModpackVersion 0.4.0
.\Launcher\test-modpack-update.ps1
```

## Publicar uma atualização

Atualize o número da versão e execute:

```powershell
.\Release\publish-release.ps1 -ModpackVersion 0.3.1
```

O script monta o pacote, compila o launcher, executa o teste completo e cria a
Release somente se todas as etapas forem concluídas.

Se uma publicação falhar somente durante o upload, repita sem recompilar os
arquivos que já passaram nos testes:

```powershell
.\Release\publish-release.ps1 -ModpackVersion 0.3.1 -SkipBuild
```

## Licenças

O código deste projeto usa a licença MIT. O pacote distribui o UE4SS com a cópia
da licença original dentro de `ue4ss/LICENSE`.
