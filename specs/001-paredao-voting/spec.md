# Feature Specification: Votação do Paredão

**Feature Branch**: `001-paredao-voting`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Vamos iniciar o projeto de votacao. Precisamos ter a capacidade de geracao de votos como um workload, o paredao sera formado por 3 individuos, aquele que tiver a maior porcentagem de votos sera o eliminado. Precisamso que a votacao fique abert por 30 segundos, e apos este periodo nao seja mais possivel inserir votos validos. deve ser exibido ao final o resultado final da votacao."

## Clarifications

### Session 2026-11-02

- Q: Como funciona a geração de votos como workload? → A: Gerador de carga externo, que simula muitos eleitores enviando votos pelo mesmo canal de votação usado por um eleitor real.
- Q: Qual vazão-alvo o workload deve sustentar? → A: 10.000 votos por segundo durante os 30 segundos de votação (~300 mil votos).
- Q: O que acontece em caso de empate na maior porcentagem? → A: O resultado declara empate entre os participantes empatados e não define eliminado automaticamente.
- Q: O que a confirmação de "voto aceito" garante ao eleitor? → A: Que o voto foi registrado de forma durável (sobrevive a falhas); a apuração pode processá-lo depois, mas ele nunca se perde.
- Q: O que acontece se o sistema cair ou reiniciar com a votação aberta? → A: O prazo original (abertura + 30 s) continua valendo; ao voltar, a votação segue aberta até esse instante ou é tratada como encerrada se ele já passou, e todos os votos confirmados são preservados e apurados.
- Q: Onde e para quem o resultado final é exibido? → A: Fica disponível para consulta por qualquer pessoa após o encerramento, e o gerador de carga o exibe ao final da execução, junto com seu relatório; não há tela visual dedicada.
- Q: Qual o estado padrão das proteções anti-abuso (ex.: limite de taxa por origem)? → A: Fora do escopo desta feature; nenhum limite por eleitor ou origem é aplicado.
- Q: Em quanto tempo o eleitor deve receber a confirmação do voto sob carga de 10.000 votos/s? → A: 95% das confirmações em até 200 ms durante o pico.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Votar durante a janela aberta (Priority: P1)

Um eleitor escolhe um dos três participantes do paredão e registra seu voto enquanto a votação está aberta. O sistema confirma que o voto foi aceito. O eleitor pode votar quantas vezes quiser, em qualquer participante, enquanto a votação estiver aberta.

**Why this priority**: Sem o registro de votos não existe paredão; é o núcleo de valor da feature.

**Independent Test**: Abrir uma votação com três participantes, enviar votos para cada um e verificar que cada voto recebe confirmação de aceite e é contabilizado.

**Acceptance Scenarios**:

1. **Given** uma votação aberta com os participantes A, B e C, **When** o eleitor vota em B, **Then** o voto é aceito, o eleitor recebe confirmação e o voto passa a contar para B.
2. **Given** uma votação aberta, **When** o mesmo eleitor vota cinco vezes em A, **Then** os cinco votos são aceitos e contabilizados para A.
3. **Given** uma votação aberta, **When** o eleitor vota em um participante que não faz parte do paredão, **Then** o voto é rejeitado com uma resposta explícita informando participante inválido.
4. **Given** uma votação aberta, **When** o mesmo voto é reenviado com a mesma chave de idempotência (ex.: retentativa por falha de conexão), **Then** o voto é contabilizado uma única vez e o reenvio recebe a mesma confirmação.

---

### User Story 2 - Encerramento automático após 30 segundos (Priority: P1)

A votação permanece aberta por exatamente 30 segundos a partir do momento da abertura. Após esse período, ela é encerrada automaticamente e nenhum voto novo é aceito; quem tenta votar recebe uma resposta explícita de que a votação está encerrada.

**Why this priority**: O prazo é uma regra de negócio central e garante a legitimidade do resultado; aceitar votos fora da janela invalida o paredão.

**Independent Test**: Abrir uma votação, enviar votos antes e depois dos 30 segundos e verificar que somente os enviados dentro da janela são aceitos e contabilizados.

**Acceptance Scenarios**:

1. **Given** uma votação aberta há 10 segundos, **When** um voto é enviado, **Then** ele é aceito.
2. **Given** uma votação aberta há mais de 30 segundos, **When** um voto é enviado, **Then** ele é rejeitado com resposta explícita de votação encerrada e não entra na contagem.
3. **Given** um paredão criado mas cuja votação ainda não foi aberta, **When** um voto é enviado, **Then** ele é rejeitado com resposta explícita de votação não iniciada.

---

### User Story 3 - Exibição do resultado final (Priority: P2)

Ao término da votação, o sistema apresenta o resultado final: total de votos válidos, quantidade e porcentagem de votos de cada participante e o participante eliminado (o de maior porcentagem). Em caso de empate na maior porcentagem, o resultado informa o empate e quem são os empatados.

**Why this priority**: O resultado é o produto final do paredão, mas depende das histórias 1 e 2 para ter dados válidos.

**Independent Test**: Executar uma votação com uma quantidade conhecida de votos por participante e verificar que o resultado exibido apresenta os totais, as porcentagens e o eliminado corretos.

**Acceptance Scenarios**:

1. **Given** uma votação encerrada com 600 votos para A, 300 para B e 100 para C, **When** o resultado final é exibido, **Then** mostra 1.000 votos no total, A com 60,00%, B com 30,00%, C com 10,00% e A como eliminado.
2. **Given** uma votação encerrada com 400 votos para A, 400 para B e 200 para C, **When** o resultado final é exibido, **Then** informa empate entre A e B e não aponta eliminado.
3. **Given** uma votação ainda aberta, **When** se solicita o resultado final, **Then** o sistema informa que o resultado só fica disponível após o encerramento.

---

### User Story 4 - Gerador de carga de votos (workload) (Priority: P2)

Um operador executa um gerador de carga externo que simula grande volume de eleitores votando simultaneamente, pelo mesmo canal usado por eleitores reais. O operador configura a taxa de envio, a duração e a distribuição dos votos entre os participantes. Ao final, o gerador apresenta um relatório com a quantidade de votos enviados, aceitos e rejeitados, permitindo conferir que a apuração bate com o que foi aceito.

**Why this priority**: É a forma de validar que o sistema mantém a contagem correta sob alto volume, objetivo de estudo do projeto; depende do registro de votos (US-1) já funcionar.

**Independent Test**: Executar o gerador contra uma votação aberta com distribuição conhecida e comparar o relatório do gerador com o resultado final da votação.

**Acceptance Scenarios**:

1. **Given** uma votação aberta, **When** o gerador é executado com taxa de 10.000 votos/s e distribuição 50%/30%/20%, **Then** o relatório do gerador indica os votos enviados, aceitos e rejeitados, exibe o resultado final da votação, e o total de aceitos é igual ao total de votos do resultado final.
2. **Given** o gerador em execução, **When** a votação é encerrada, **Then** os votos enviados após o encerramento aparecem como rejeitados no relatório e não constam no resultado.
3. **Given** o gerador configurado para reenviar parte dos votos com a mesma chave de idempotência, **When** a execução termina, **Then** nenhum voto reenviado é contado em duplicidade no resultado.

---

### Edge Cases

- Voto recebido exatamente no limite dos 30 segundos: o instante de recebimento registrado pelo sistema decide; votos recebidos até o instante de encerramento (inclusive) são aceitos, e posteriores são rejeitados.
- Votação encerrada sem nenhum voto: o resultado mostra total zero, 0,00% para todos e nenhum eliminado.
- Participante sem votos: aparece no resultado com 0 votos e 0,00%.
- Porcentagens com dízima (ex.: 1/3): exibidas com duas casas decimais; o critério de eliminação usa a contagem exata de votos, não o valor arredondado.
- Empate entre os três participantes: o resultado informa empate entre os três.
- Reenvio de voto com chave de idempotência já usada, mas com participante diferente: rejeitado como conflito, sem alterar o voto original.
- Tentativa de abrir uma nova votação enquanto outra está aberta ou ainda não finalizada (resultado não apurado): rejeitada.
- Sistema sobrecarregado: o voto **não** é confirmado; o eleitor recebe resposta explícita de sobrecarga e pode reenviar com a mesma chave de idempotência sem risco de duplicidade.
- Queda ou reinício do sistema com a votação aberta: o instante de encerramento original não muda; ao retornar, votos são aceitos apenas se recebidos antes desse instante, e os votos confirmados antes da queda constam no resultado.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir criar um paredão composto por exatamente 3 participantes distintos.
- **FR-002**: O sistema MUST permitir que um operador abra a votação de um paredão; a abertura marca o início da janela de votação.
- **FR-003**: A votação MUST permanecer aberta por exatamente 30 segundos a partir da abertura e ser encerrada automaticamente ao fim desse período, sem ação manual.
- **FR-004**: O sistema MUST aceitar votos somente enquanto a votação estiver aberta.
- **FR-005**: O sistema MUST rejeitar com resposta explícita, identificando o motivo, votos recebidos antes da abertura, após o encerramento ou destinados a participante que não pertence ao paredão. Quando sobrecarregado, MUST responder explicitamente que o voto não foi confirmado.
- **FR-006**: O sistema MUST permitir que um mesmo eleitor vote múltiplas vezes, sem restrição de voto único.
- **FR-007**: Cada voto MUST carregar uma chave de idempotência; reenvios com a mesma chave MUST resultar em uma única contabilização e receber a mesma confirmação do envio original.
- **FR-008**: Cada voto aceito MUST ser contabilizado exatamente uma vez: nenhum voto confirmado pode ser perdido nem duplicado. A confirmação de aceite MUST ser emitida somente depois que o voto estiver registrado de forma durável, de modo que sobreviva a uma falha do sistema; a apuração pode processar o voto posteriormente.
- **FR-009**: Votos aceitos MUST ser registros imutáveis; totais e porcentagens MUST ser derivados dos votos registrados, nunca editados diretamente.
- **FR-010**: Após o encerramento, o sistema MUST apurar o resultado calculando, para cada participante, a quantidade de votos e a porcentagem sobre o total de votos válidos.
- **FR-011**: O participante com a maior quantidade de votos (e, portanto, a maior porcentagem) MUST ser declarado eliminado.
- **FR-012**: Em caso de empate na maior quantidade de votos, o resultado MUST declarar empate, listar os participantes empatados e não apontar eliminado.
- **FR-013**: Após o término da votação, o sistema MUST disponibilizar o resultado final para consulta por qualquer pessoa, sem autenticação, contendo total de votos válidos, votos e porcentagem por participante (duas casas decimais) e o eliminado ou o empate. Uma tela visual dedicada está fora do escopo.
- **FR-014**: O resultado final MUST ficar indisponível enquanto a votação estiver aberta, com resposta explícita informando isso.
- **FR-015**: O projeto MUST incluir um gerador de carga externo que envia votos pelo mesmo canal usado por eleitores reais, com taxa de envio, duração e distribuição entre participantes configuráveis.
- **FR-016**: O gerador de carga MUST produzir, ao final da execução, um relatório com a quantidade de votos enviados, aceitos e rejeitados (por motivo) e o tempo de resposta observado (incluindo o percentil 95), e MUST exibir junto o resultado final consultado da votação, permitindo conferir os aceitos com o total apurado.
- **FR-017**: O gerador de carga MUST suportar o reenvio de votos com a mesma chave de idempotência, para exercitar o cenário de retentativas.
- **FR-018**: O sistema MUST NOT impor limite de votos por eleitor ou por origem nesta feature; votos múltiplos vindos da mesma origem, incluindo toda a carga do gerador, MUST ser aceitos enquanto a votação estiver aberta. Proteções anti-abuso ficam fora do escopo.
- **FR-019**: O sistema MUST permitir somente uma votação aberta por vez; uma nova votação só pode ser aberta depois que a anterior for finalizada (resultado apurado).
- **FR-020**: Após queda ou reinício, o sistema MUST manter o instante de encerramento original da votação (abertura + 30 s), sem pausar nem estender o prazo, e MUST preservar todos os votos confirmados antes da falha.

### Key Entities

- **Paredão**: disputa de eliminação; contém exatamente 3 participantes, o instante de abertura, o instante de encerramento (abertura + 30 s) e o estado (criado, aberto, encerrado, finalizado — resultado apurado).
- **Participante**: indivíduo concorrendo no paredão; identificado de forma única dentro do paredão e possui um nome de exibição.
- **Voto**: registro imutável de uma escolha; referencia o paredão e o participante, guarda o instante de recebimento e a chave de idempotência.
- **Resultado**: apuração derivada dos votos de um paredão encerrado; contém o total de votos válidos, votos e porcentagem por participante e o eliminado ou a lista de empatados.
- **Execução do Workload**: uma rodada do gerador de carga; contém os parâmetros (taxa, duração, distribuição, fração de reenvios) e os contadores de votos enviados, aceitos e rejeitados, além do tempo de resposta observado (incluindo o percentil 95).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O sistema sustenta 10.000 votos por segundo durante os 30 segundos de votação (~300 mil votos) sem perder nenhum voto aceito.
- **SC-002**: Em 100% das execuções do gerador, o total de votos aceitos no relatório é igual ao total de votos válidos do resultado final.
- **SC-003**: 0 votos recebidos após o encerramento são contabilizados no resultado.
- **SC-004**: 0 votos são contados em duplicidade quando reenviados com a mesma chave de idempotência.
- **SC-005**: O resultado final fica disponível em até 5 segundos após o encerramento da votação.
- **SC-006**: As porcentagens exibidas no resultado somam 100% (com tolerância de ±0,01 ponto percentual por arredondamento) sempre que houver ao menos um voto.
- **SC-007**: 100% dos votos rejeitados recebem uma resposta explícita com o motivo da rejeição.
- **SC-008**: Sob carga de 10.000 votos por segundo, 95% dos eleitores recebem a confirmação (ou rejeição) do voto em até 200 ms.

## Assumptions

- Há somente um paredão com votação aberta por vez.
- Eleitores são anônimos; autenticação e identificação de eleitores estão fora do escopo.
- A abertura da votação é acionada por um operador; o encerramento é automático.
- A duração de 30 segundos é fixa nesta feature.
- Resultados parciais durante a votação estão fora do escopo; apenas o resultado final é exibido.
- Critérios de desempate estão fora do escopo; o empate é apenas declarado.
- Proteções anti-abuso (rate limiting, detecção de bots) estão fora do escopo; quando forem introduzidas em feature futura, deverão ser configuráveis e não bloquear o voto múltiplo legítimo, conforme a constitution.
- O relógio do sistema que recebe os votos é a referência para decidir se um voto está dentro da janela.
- O gerador de carga é uma ferramenta de teste/estudo operada localmente, não um canal de votação de produção.
