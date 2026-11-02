# Constitution do Voting

Sistema de votação para eliminação de participantes em programas de TV (estilo "paredão").
Projeto de **estudo**: o objetivo é praticar arquitetura e TDD em um cenário de alta
concorrência, sem compromisso com produção.

## Princípios Fundamentais

### I. Clean Architecture

- A solução DEVE ser organizada em camadas: `Voting.Domain`, `Voting.Application`,
  `Voting.Infrastructure` e `Voting.Api`.
- Dependências apontam SEMPRE para dentro: Domain não referencia nenhuma outra camada
  nem pacotes de infraestrutura (EF Core, ASP.NET, brokers).
- Regras de negócio (abertura/encerramento de votação, validação de participante,
  apuração) DEVEM viver em Domain/Application, nunca em controllers ou repositórios.
- Infraestrutura é acessada via interfaces declaradas em Application.

**Racional**: isolar o domínio permite testá-lo sem I/O e trocar a infraestrutura
(banco, fila, cache) como exercício de aprendizado.

### II. Test-Driven Development (INEGOCIÁVEL)

- Todo comportamento novo DEVE começar por um teste que falha (Red → Green → Refactor).
- Testes de unidade cobrem Domain e Application; testes de integração cobrem API e
  Infrastructure contra dependências reais (Testcontainers), sem mocks de banco.
- Nenhum PR é aceito com testes falhando, ignorados (`Skip`) sem justificativa ou
  com código de produção sem teste correspondente.
- Nomes de testes descrevem o comportamento: `Method_Scenario_ExpectedResult`.

**Racional**: TDD é um dos objetivos de estudo do projeto e garante que a apuração
de votos esteja correta.

### III. Integridade da Contagem de Votos

- Um eleitor PODE votar quantas vezes quiser; NÃO existe restrição de voto único.
- Cada voto aceito DEVE ser contabilizado **exatamente uma vez**: nenhum voto confirmado
  pode ser perdido nem duplicado por retry. Requisições carregam uma chave de
  idempotência.
- Votos são registros imutáveis (append-only). Totais e percentuais são derivados
  dos votos, nunca editados diretamente.
- Votos só são aceitos com a votação **aberta**; votos recebidos após o encerramento
  DEVEM ser rejeitados com resposta explícita.
- O sistema DEVE suportar picos de alta vazão. Estratégias de escala (fila,
  processamento assíncrono, agregação) são permitidas, desde que preservem as
  garantias acima.
- Proteções anti-abuso (rate limiting, detecção de bots) DEVEM ser configuráveis e
  não podem bloquear o voto múltiplo legítimo.

**Racional**: a regra do domínio permite voto múltiplo, então a confiabilidade está
na contagem correta, e não na unicidade do eleitor.

### IV. Qualidade Garantida pelo Build

- `Directory.Build.props` e `.editorconfig` são a fonte da verdade do estilo; o build
  DEVE terminar com **zero warnings**.
- Nullable reference types habilitado; supressões (`!`, `#pragma`) exigem comentário
  justificando.
- Todo código, comentário, log, mensagem de commit e teste fica em inglês; a
  documentação (specs, plans, README) fica em português.

**Racional**: regras automatizadas evitam discussões subjetivas de estilo em revisão.

## Restrições Técnicas

- Plataforma: .NET 10 (LTS), ASP.NET Core, C# na versão mais recente suportada.
- Testes: xUnit, com Testcontainers para integração.
- Persistência, mensageria e cache: definidas em cada `/speckit.plan`, justificadas
  pelos requisitos da feature.
- Simplicidade: por ser um projeto de estudo, cada nova dependência ou padrão
  arquitetural DEVE ter justificativa no plan (tabela "Complexity Tracking").
- Segredos nunca são versionados (usar user-secrets ou variáveis de ambiente).

## Fluxo de Desenvolvimento

### Sequência obrigatória

`/speckit.specify` → `/speckit.clarify` → `/speckit.plan` → `/speckit.tasks` →
`/speckit.analyze` → `/speckit.implement`

- `/speckit.analyze` é OBRIGATÓRIO antes de `/speckit.implement`. Achados de
  severidade CRITICAL ou HIGH DEVEM ser resolvidos (ajustando spec, plan ou tasks)
  antes de iniciar a implementação.
- O plan DEVE passar pelo "Constitution Check" antes da Fase 0 e depois do design.

### Rastreabilidade: spec → plan → tasks

- **Etapas do plan** são identificadas pelo prefixo `PLN-` seguido de dois dígitos
  sequenciais (`PLN-01`, `PLN-02`, ...).
- Cada etapa do plan DEVE ser justificada pela especificação, citando ao menos um
  requisito ou história da spec (ex.: `PLN-03 — Justificativa: FR-004, US-2`).
  Etapas sem justificativa na spec NÃO são permitidas.
- **Tasks** usam o ID da etapa do plan à qual pertencem, com um sufixo numérico
  sequencial de três dígitos: `PLN-03-001`, `PLN-03-002`, ...
- NÃO pode existir task sem etapa de plan atribuída (task órfã). O
  `/speckit.analyze` DEVE sinalizar como CRITICAL qualquer task órfã ou etapa de plan
  sem justificativa.
- Dentro de cada etapa, as tasks de teste vêm antes das tasks de implementação
  (Princípio II).

### Entrega

- Commits pequenos, seguindo Conventional Commits (em inglês), citando o ID da task
  (ex.: `feat(votes): register vote with idempotency key [PLN-03-002]`).
- Um PR só é aceito com build sem warnings, todos os testes passando e conformidade
  com esta constitution.

## Governance

- Esta constitution prevalece sobre qualquer outra prática do projeto.
- **Conflito plan × constitution**: se um plan (ou spec/tasks) contradiz esta
  constitution, quem muda é o plan. A constitution NUNCA é alterada para acomodar um
  plan dentro do fluxo de uma feature.
- **Emendas em commit separado**: alterações nesta constitution DEVEM ser feitas em
  commit próprio, que contém apenas este arquivo e os templates afetados, sem misturar
  código, spec, plan ou tasks (ex.: `docs(constitution): amend principle III [v1.1.0]`).
- Uma feature que dependa de uma emenda só prossegue depois que o commit da emenda
  existir; em seguida, o plan é revalidado no "Constitution Check".
- Versionamento semântico:
  MAJOR: remoção ou redefinição incompatível de princípio;
  MINOR: novo princípio ou seção;
  PATCH: esclarecimentos e redação.
- Violações justificadas são registradas em "Complexity Tracking" no plan da feature.
- Toda revisão de PR DEVE verificar a conformidade com esta constitution.

**Version**: 1.0.0 | **Ratified**: 2026-11-02 | **Last Amended**: 2026-11-02
