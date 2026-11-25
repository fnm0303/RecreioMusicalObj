# Recreio Musical

Aplicativo web para montar, em conjunto, a playlist do recreio: **cinco músicas**, escolhidas por qualquer pessoa, sem login. A busca usa a API do Spotify e cada música é creditada ao apelido de quem escolheu.

> **Status:** Fase 1 de 5 concluída — fundação, arquitetura, página inicial e testes.
> A busca no Spotify e a playlist funcional entram nas próximas fases.

## Stack

- ASP.NET Core MVC (.NET 10) com Razor/cshtml
- Clean Architecture em quatro camadas
- SQLite + EF Core (Fase 3)
- SSE para sincronizar a playlist entre dispositivos (Fase 4)
- Spotify Web API via *Client Credentials* — sem login do usuário
- xUnit para testes

## Estrutura

```text
RecreioMusical.sln
src/
  RecreioMusical.Domain/          regras puras (capacidade de 5 vagas, entidades)
  RecreioMusical.Application/     casos de uso e contratos (sem EF, HTTP ou ASP.NET)
  RecreioMusical.Infrastructure/  EF Core/SQLite, token e catálogo do Spotify
  RecreioMusical.Web/             MVC + Razor, controllers, SSE e assets
tests/
  RecreioMusical.Tests/           regras, arquitetura e smoke test da página
```

Direção das dependências: `Domain ← Application ← Infrastructure/Web`.
Os testes de arquitetura garantem que `Domain` e `Application` não conhecem as camadas externas.

## Pré-requisitos

- .NET SDK **10.0.203** (o `global.json` fixa a versão; `dotnet --version` deve retornar `10.0.x`)
- Credenciais de um app do Spotify (necessárias a partir da Fase 2)
- Navegador moderno

## Como rodar

```powershell
dotnet restore
dotnet build
dotnet run --project src/RecreioMusical.Web
```

Acesse <http://localhost:5209> (perfil `http`). O perfil `https` usa <https://localhost:7000>.

Também é possível abrir `RecreioMusical.sln` em uma IDE com suporte a .NET 10 e iniciar o projeto `RecreioMusical.Web`.

## Configuração de segredos

Nenhum segredo fica no repositório. Os valores são lidos de user-secrets em desenvolvimento e de variáveis de ambiente em produção.

### Spotify (necessário a partir da Fase 2)

1. Acesse <https://developer.spotify.com/dashboard> e faça login.
2. Crie um app (*Create app*). O formulário exige uma *Redirect URI*; use `http://localhost:5209/callback` — ela não é usada pelo fluxo *Client Credentials*.
3. Marque **Web API** e salve. Copie o **Client ID** e o **Client Secret**.
4. Grave os valores fora do repositório:

```powershell
dotnet user-secrets set "Spotify:ClientId" "SEU_CLIENT_ID" --project src/RecreioMusical.Web
dotnet user-secrets set "Spotify:ClientSecret" "SEU_CLIENT_SECRET" --project src/RecreioMusical.Web
```

Em produção, use as variáveis de ambiente `Spotify__ClientId` e `Spotify__ClientSecret`.

> O app sobe normalmente sem as credenciais: a página exibe um aviso em desenvolvimento e a busca fica desabilitada. Nenhum segredo é enviado ao navegador.

### PIN administrativo

Remover músicas e limpar a playlist exigirá PIN (Fase 4). Neste ambiente já existe um valor provisório `1234`:

```powershell
dotnet user-secrets set "Admin:Pin" "1234" --project src/RecreioMusical.Web
```

Troque antes de divulgar o app e use algo mais forte que quatro dígitos, se possível.
Sem `Admin:Pin` configurado, as ações administrativas permanecerão desativadas.

## Testes

```powershell
dotnet test
```

Cobertura atual:

- `PlaylistRulesTests` — capacidade de cinco músicas e faixa de vagas.
- `ArchitectureTests` — `Domain` e `Application` não referenciam camadas externas.
- `HomePageTests` — a página inicial responde 200 e renderiza as cinco vagas.

## Rotas

| Rota | Status | Fase |
| --- | --- | --- |
| `GET /` | Implementada (shell) | 1 |
| `GET /api/tracks/search?q=` | Planejada | 2–3 |
| `GET /api/playlist` | Planejada | 3 |
| `POST /api/playlist` | Planejada | 3 |
| `DELETE /api/playlist/{slot}` | Planejada (PIN) | 3–4 |
| `DELETE /api/playlist` | Planejada (PIN) | 3–4 |
| `GET /api/playlist/events` | Planejada (SSE) | 4 |

## Decisões de arquitetura

- **Domain** contém apenas as regras que não dependem de tecnologia. A capacidade de 5 vagas é uma constante de domínio, não uma configuração de UI.
- **Application** orquestra casos de uso e expõe contratos (`ISpotifyCatalog`, `IPlaylistRepository`, `IPlaylistNotifier`), sem saber como são implementados.
- **Infrastructure** implementa persistência e HTTP; o token do Spotify é obtido no servidor e nunca exposto ao cliente.
- **Web** é a raiz de composição: registra DI, configura o pipeline e entrega Razor/JSON.
- **Cinco vagas fixas**: o banco terá índice único em `Slot` (1–5), garantindo o limite mesmo com adições simultâneas.
- **Sem login**: a playlist é pública e o PIN protege somente remoção e limpeza.
- **Reprodução**: apps novos do Spotify não retornam mais `preview_url`; o player oficial embed será usado, sem prometer áudio em todas as faixas.

## Roadmap

- [x] **Fase 1 — Fundação:** solução, quatro camadas + testes, DI, configuração, página inicial e README.
- [ ] **Fase 2 — Domínio e casos de uso:** `PlaylistItem`, `Nickname`, `Result`, handlers de busca/adição/remoção e testes unitários.
- [ ] **Fase 3 — Infraestrutura:** EF Core + SQLite + migrations, repositório com 5 slots, token/catálogo do Spotify, cache e tratamento de `429`.
- [ ] **Fase 4 — Web/UI:** busca com debounce, apelido persistido, adição/remoção com PIN, SSE e player embed.
- [ ] **Fase 5 — Fechamento:** testes de integração dos endpoints, rate limit, acessibilidade, README final e checklist de aceite.

## Observações

- A página inicial em `http://localhost:5209` é estática por enquanto: os controles aparecem desabilitados de propósito.
- O aviso amarelo só aparece em `Development`; ele resume o que falta configurar.
- Arquivos SQLite estão no `.gitignore`; user-secrets ficam no perfil do usuário, fora do projeto.
