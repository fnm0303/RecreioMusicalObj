# Recreio Musical

Aplicativo web para montar, em conjunto, a playlist do recreio: **cinco músicas**, escolhidas por qualquer pessoa, sem login. A busca usa a API do Spotify e cada música é creditada ao apelido de quem escolheu.

> **Status:** Fase 5 pronta para go-live — busca no Spotify, playlist ao vivo por SSE, player embed, administração por PIN e publicação no Azure App Service, com 132 testes.

## Stack

- ASP.NET Core MVC (.NET 10) com Razor/cshtml
- Clean Architecture em quatro camadas
- SQLite + EF Core com migrations
- SSE para sincronizar a playlist entre dispositivos
- Spotify Web API via *Client Credentials* — sem login do usuário
- xUnit para testes

## Estrutura

```text
RecreioMusical.sln
src/
  RecreioMusical.Domain/          regras puras: PlaylistItem, Nickname, TrackInfo e 5 vagas
  RecreioMusical.Application/     casos de uso, Result e contratos (sem EF, HTTP ou ASP.NET)
  RecreioMusical.Infrastructure/  EF Core/SQLite, token, catálogo do Spotify e hub de mudanças
  RecreioMusical.Web/             MVC + Razor, controllers, SSE e assets
tests/
  RecreioMusical.Tests/           domínio, casos de uso, infraestrutura, arquitetura e smoke test
```

Direção das dependências: `Domain ← Application ← Infrastructure/Web`.
Os testes de arquitetura garantem que `Domain` e `Application` não conhecem as camadas externas.

## Pré-requisitos

- .NET SDK **10.0.203** (o `global.json` fixa a versão; `dotnet --version` deve retornar `10.0.x`)
- Credenciais de um app do Spotify
- Navegador moderno

## Como rodar

```powershell
dotnet restore
dotnet build
dotnet run --project src/RecreioMusical.Web
```

Acesse <http://localhost:5209> (perfil `http`). O perfil `https` usa <https://localhost:7000>.

Também é possível abrir `RecreioMusical.sln` em uma IDE com suporte a .NET 10 e iniciar o projeto `RecreioMusical.Web`.

## Publicação no Azure

O ambiente de produção usa **Azure App Service (Linux, .NET 10, plano B1)** e **GitHub Actions com OIDC**. O workflow `.github/workflows/azure-deploy.yml` roda build, os 132 testes e publica a branch `main` a cada push.

### Provisionamento (uma vez)

Pré-requisitos: assinatura Azure com permissão para criar recursos e *app registrations*, credenciais do Spotify e um PIN forte (6 a 12 dígitos).

No **Azure Cloud Shell**:

```bash
git clone https://github.com/fnm0303/RecreioMusicalObj.git
cd RecreioMusicalObj
bash scripts/azure-provision.sh
```

O script cria o resource group, o plano B1 e o Web App; configura HTTPS Only, TLS 1.2, Always On, health check em `/health` e logs; grava as app settings (Spotify, PIN e `ConnectionStrings__Playlist=Data Source=/home/data/recreio-musical.db`); cria a identidade federada OIDC para `repo:fnm0303/RecreioMusicalObj:ref:refs/heads/main` com o papel `Website Contributor`; e imprime os valores do GitHub. Se o `gh` estiver autenticado, ele grava os secrets e dispara o workflow de deploy automaticamente.

Cadastro manual no GitHub (Settings → Secrets and variables → Actions):

| Tipo | Nome | Valor |
| --- | --- | --- |
| Secret | `AZURE_CLIENT_ID` | appId da identidade criada |
| Secret | `AZURE_TENANT_ID` | tenant da assinatura |
| Secret | `AZURE_SUBSCRIPTION_ID` | id da assinatura |
| Variable | `AZURE_WEBAPP_NAME` | nome do Web App (ex.: `recreio-musical-obj`) |

### Deploy

Faça push na `main` ou execute **Actions → Deploy to Azure App Service → Run workflow**. Enquanto a variable `AZURE_WEBAPP_NAME` não existir, o workflow compila e testa, mas pula o deploy — assim o repositório já pode receber o script de provisionamento sem quebrar o CI. Para acompanhar:

```bash
az webapp log tail --name recreio-musical-obj --resource-group rg-recreio-musical
```

### Operação

| Item | Valor |
| --- | --- |
| URL | `https://recreio-musical-obj.azurewebsites.net` |
| Banco | `/home/data/recreio-musical.db` |
| Backups | `/home/data/backups` (um por dia, mantendo 7) |
| Saúde | `/health` |
| Instâncias | 1 — o hub do SSE e o rate limit do PIN são em memória |
| Custo | B1 Linux, aproximadamente US$ 13–16/mês |

- **Trocar o PIN:** `az webapp config appsettings set -g rg-recreio-musical -n recreio-musical-obj --settings Admin__Pin=NOVO_PIN` (o app reinicia).
- **Reiniciar:** `az webapp restart -g rg-recreio-musical -n recreio-musical-obj`.
- **Baixar um backup:** Kudu em `https://recreio-musical-obj.scm.azurewebsites.net` → *File Manager* → `/home/data/backups`.
- **Restaurar:** `az webapp stop`, substitua o `.db` pelo backup, `az webapp start`.
- **Logs:** `az webapp log tail` ou *Log stream* no portal.
- **Orçamento:** em *Cost Management → Budgets*, crie um alerta (ex.: US$ 20/mês, aviso em 80%) para evitar surpresa na fatura.
- **Domínio próprio (opcional):** adicione o CNAME e o TXT de verificação no DNS da escola; o certificado gerenciado gratuito é emitido pelo App Service.

O `/home` do App Service é um compartilhamento de rede (Azure Files/SMB). Com uma instância e escrita rara — cinco vagas — o SQLite funciona bem, e o backup diário cobre o risco. Se o uso crescer, o próximo passo é migrar para Azure SQL.

O plano B1 **não tem deployment slots**: cada deploy reinicia o app por alguns segundos; o SSE reconecta sozinho. Evite publicar durante o recreio. Para zero downtime, seria necessário o tier Standard.

## Banco de dados

O app usa SQLite e aplica as migrations automaticamente na inicialização. Por padrão o arquivo fica em `src/RecreioMusical.Web/recreio-musical.db` (resolvido a partir do content root); o caminho pode ser trocado em `ConnectionStrings:Playlist`.

```powershell
# criar uma nova migration depois de mudar o modelo
dotnet ef migrations add NomeDaMigration --project src/RecreioMusical.Infrastructure --output-dir Persistence/Migrations

# aplicar manualmente (o app também aplica ao subir)
dotnet ef database update --project src/RecreioMusical.Infrastructure

# recomeçar do zero: pare o app e apague o arquivo
Remove-Item src/RecreioMusical.Web/recreio-musical.db*
```

O esquema garante no banco as invariantes críticas: chave primária em `Slot` (1–5, com `CHECK`) e índice único em `TrackId`. Os arquivos `*.db`, `*.db-shm` e `*.db-wal` estão no `.gitignore`.

## Configuração de segredos

Nenhum segredo fica no repositório. Os valores são lidos de user-secrets em desenvolvimento e de variáveis de ambiente em produção.

### Spotify

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

Remover músicas e limpar a playlist exige PIN. Em desenvolvimento ele fica em user-secrets, fora do repositório:

```powershell
dotnet user-secrets set "Admin:Pin" "19031984" --project src/RecreioMusical.Web
```

Troque antes de divulgar o app e use algo mais forte que quatro dígitos, se possível.
Sem `Admin:Pin` configurado, as ações administrativas permanecerão desativadas.

## Testes

```powershell
dotnet test
```

Cobertura atual — **132 testes**:

- `NicknameTests` — normalização de espaços, limites de 2–30 caracteres e códigos de erro.
- `TrackInfoTests` — campos obrigatórios, URLs absolutas e limites de tamanho.
- `PlaylistItemTests` — associação de vaga, faixa, apelido e horário; vagas 1–5.
- `PlaylistRulesTests` — capacidade, menor vaga livre, playlist cheia e vagas inválidas.
- `SearchTracksHandlerTests` — termo mínimo, trim, limite e falha do Spotify.
- `AddTrackHandlerTests` — primeira vaga, buraco, playlist cheia, duplicata, apelido/ID inválidos, faixa reconfirmada, erro do Spotify e corrida de vaga.
- `RemoveTrackHandlerTests`, `ClearPlaylistHandlerTests`, `GetPlaylistHandlerTests` — remoção, limpeza, snapshot publicado e ordenação.
- `EfPlaylistRepositoryTests` — persistência real em SQLite, ordenação, duplicata, vaga ocupada, reuso de vaga, remoção e limpeza.
- `SpotifyCatalogServiceTests` — mapeamento de busca/faixa, capa, filtros, `market`/`limit`, retry de `401` e `429`, erros e JSON inválido.
- `SpotifyTokenProviderTests` — Client Credentials, Basic auth, cache, expiração, invalidação e falhas.
- `PlaylistChangeHubTests` — snapshot inicial e distribuição de mudanças para assinantes.
- `DependencyInjectionTests` — grafo completo de DI validado na composição.
- `PlaylistApiTests` — endpoints reais via `WebApplicationFactory`: playlist vazia, adição com apelido, apelido inválido, duplicata, sexta música, PIN ausente/errado/correto, limpeza, rate limit, busca e snapshot inicial do SSE.
- `ProductionReadinessTests` — `/health`, `X-Robots-Tag`/`robots.txt` e criação automática do diretório do SQLite.
- `ArchitectureTests` e `HomePageTests` — direção das dependências e página inicial.

## Regras implementadas

- **Apelido:** obrigatório, normalizado (espaços colapsados) e entre 2 e 30 caracteres.
- **Vaga:** a primeira livre é ocupada, preenchendo buracos antes de avançar; o máximo é 5.
- **Faixa reconfirmada:** o cliente envia apenas o ID; título, artistas, capa e link vêm do catálogo do Spotify no servidor.
- **Duplicata:** a mesma faixa não entra duas vezes.
- **Concorrência:** até três tentativas quando alguém ocupa a vaga entre a leitura e a inserção; a chave primária e o índice único do SQLite são a garantia final.
- **Spotify:** token *Client Credentials* em cache até pouco antes de expirar; `401` renova o token e `429` respeita o `Retry-After`, cada um com uma nova tentativa. Falhas viram `SpotifyUnavailable`.
- **Erros previsíveis:** os casos de uso retornam `Result` com código estável e mensagem em PT-BR, prontos para os controllers mapearem status HTTP.
- **Notificação:** adicionar, remover e limpar publicam o snapshot da playlist no `PlaylistChangeHub`; o SSE em `/api/playlist/events` apenas assina esse hub.
- **Administração:** remoção e limpeza exigem o header `X-Admin-Pin`. O PIN é comparado em tempo constante (hash SHA-256), com limite de 5 tentativas erradas por origem a cada 5 minutos (`429` + `Retry-After`). Sem `Admin:Pin`, as ações administrativas ficam desativadas (`403`).
- **Tela:** busca com debounce e cancelamento de requisições, apelido salvo no navegador, cinco vagas renderizadas no servidor e atualizadas via SSE, player oficial embed do Spotify e modal de PIN.

## Rotas

| Rota | Descrição |
| --- | --- |
| `GET /` | Página da playlist, renderizada no servidor |
| `GET /api/tracks/search?q=` | Busca no catálogo do Spotify (2–80 caracteres) |
| `GET /api/playlist` | Estado atual da playlist |
| `POST /api/playlist` | Adiciona uma faixa (`trackId` + `addedBy`) |
| `DELETE /api/playlist/{slot}` | Remove a música da vaga; exige `X-Admin-Pin` |
| `DELETE /api/playlist` | Esvazia a playlist; exige `X-Admin-Pin` |
| `GET /api/playlist/events` | SSE com o snapshot inicial e cada mudança |

Erros da API usam *Problem Details* com `status`, `title`, `detail`, `instance` e o código estável em `code`. O SSE envia eventos `playlist` e um comentário de keep-alive a cada 15 segundos.

## Decisões de arquitetura

- **Domain** contém apenas as regras que não dependem de tecnologia. A capacidade de 5 vagas é uma constante de domínio, não uma configuração de UI.
- **Application** orquestra casos de uso e expõe contratos (`ISpotifyCatalogService`, `IPlaylistRepository`, `IPlaylistChangeNotifier`), sem saber como são implementados.
- **Infrastructure** implementa persistência, HTTP e o hub de mudanças; o token do Spotify é obtido no servidor e nunca exposto ao cliente.
- **Web** é a raiz de composição: registra DI, configura o pipeline, aplica migrations e entrega Razor/JSON.
- **Cinco vagas fixas**: o banco tem chave primária em `Slot` (1–5) e índice único em `TrackId`, garantindo o limite e a ausência de duplicatas mesmo com adições simultâneas.
- **Entidade separada:** `PlaylistItemEntity` mapeia a tabela e é convertida para o `PlaylistItem` do domínio, que continua sem atributos de EF.
- **Sem login**: a playlist é pública e o PIN protege somente remoção e limpeza.
- **Identidade visual**: tema azul-céu/ciano com tokens em `site.css`; a logo do Colégio Objetivo fica no cabeçalho dentro de uma plaquinha clara, para contraste sobre o fundo escuro, e escala nos breakpoints de 900, 700, 430 e 360 px.
- **Reprodução**: apps novos do Spotify não retornam mais `preview_url`; o player oficial embed será usado, sem prometer áudio em todas as faixas.

## Roadmap

- [x] **Fase 1 — Fundação:** solução, quatro camadas + testes, DI, configuração, página inicial e README.
- [x] **Fase 2 — Domínio e casos de uso:** `PlaylistItem`, `Nickname`, `TrackInfo`, `Result`, handlers de busca/adição/remoção/limpeza e 76 testes.
- [x] **Fase 3 — Infraestrutura:** EF Core + SQLite + migration inicial, repositório com as invariantes no banco, token/catálogo do Spotify com cache e retry, hub de notificações e handlers no contêiner; 116 testes.
- [x] **Fase 4 — Web/UI:** busca com debounce, apelido persistido, adição/remoção com PIN, SSE, player embed e endpoints `/api`; 129 testes.
- [x] **Fase 5 — Publicação:** forwarded headers, `/health`, `noindex`, banco em `/home/data`, backup diário, workflow OIDC e script de provisionamento; 132 testes.
- [ ] **Go-live:** provisionar o Azure, publicar a `main` e validar a URL pública em dois dispositivos.

## Observações

- A página inicial em `http://localhost:5209` já está funcional: busca, playlist ao vivo e administração por PIN.
- As migrations são aplicadas automaticamente ao iniciar; o arquivo SQLite fica no content root (`src/RecreioMusical.Web` por padrão) e é ignorado pelo git.
- Os handlers estão registrados no contêiner junto com o repositório SQLite, o catálogo do Spotify e o hub de mudanças. O teste `DependencyInjectionTests` valida o grafo completo.
- Sem credenciais do Spotify o app sobe normalmente; a busca fica desabilitada e o aviso amarelo aparece só em `Development`.
- user-secrets ficam no perfil do usuário, fora do projeto.
