# Pulso I/O — Contexto permanente do produto

> Este documento é a fonte de verdade funcional e técnica do Pulso I/O.
> Deve ser lido antes de planejar, criar ou alterar funcionalidades. Quando uma
> decisão do produto mudar, este arquivo também deve ser atualizado.

## 1. Resumo executivo

O **Pulso I/O** é uma plataforma SaaS B2B para monitorar a saúde de integrações
entre sistemas. O produto deve acompanhar tanto os dados que uma aplicação do
cliente **recebe** quanto os dados que ela **consome ou envia**, oferecendo
visibilidade operacional, alertas, rastreabilidade por transação e informações
técnicas suficientes para diagnóstico.

O produto nasceu da experiência prática de Gustavo Moitinho com integrações,
APIs, web services, telemetria, regras, suporte operacional e investigação de
falhas. A proposta não é substituir TMS, gerenciadoras de risco, rastreadores ou
os sistemas de negócio dos clientes. O Pulso I/O será uma camada complementar
de observabilidade e diagnóstico.

Embora a ideia tenha surgido em operações logísticas, a marca e a arquitetura
**não devem limitar o produto à logística**. O objetivo é atender qualquer
empresa que dependa de integrações entre sistemas.

## 2. Identidade do produto

- Nome oficial: **Pulso I/O**
- Forma simplificada: **Pulso IO**
- Identificador sugerido para código, domínio e redes: `pulsoio`
- Tagline provisória: **A saúde das suas integrações.**
- Personalidade: moderna, confiável, técnica e clássica ao mesmo tempo
- Paleta aprovada conceitualmente: grafite, marfim e violeta
- Evitar: combinação azul e laranja, por associação visual com a Ravex
- Direção visual aprovada: primeiro conceito de logo apresentado na conversa
- Assets raster oficiais recebidos: símbolo, nome e composição completa da marca,
  versionados em `src/Web/public/assets/brand`

O nome foi mantido mesmo após uma triagem preliminar apontar usos frequentes do
termo “Pulso” em tecnologia. Antes da divulgação comercial, ainda será
necessária uma busca formal no INPI, principalmente nas classes 9 e 42, além de
verificação de domínio e identificadores sociais.

## 3. Problema que o produto resolve

Empresas que dependem de integrações frequentemente enfrentam:

- quedas e instabilidades percebidas somente após impacto operacional;
- falta de uma visão central de integrações de entrada e saída;
- logs dispersos, excessivamente técnicos ou sem correlação;
- dependência constante de desenvolvedores para investigar ocorrências;
- dificuldade para localizar quando, onde e por que uma transação falhou;
- endpoints online que, apesar disso, não estão processando o fluxo esperado;
- respostas técnicas pouco objetivas para equipes operacionais e clientes;
- ausência de regras de saúde específicas para cada integração.

O Pulso I/O deve transformar esses sinais em uma resposta objetiva:

1. A integração está disponível?
2. Ela está realmente movimentando dados?
3. Está operando dentro das regras esperadas?
4. Qual transação falhou, quando e em qual etapa?
5. Qual é a explicação operacional e qual é o detalhe técnico?

## 4. Posicionamento

O Pulso I/O é uma plataforma de **monitoramento, diagnóstico e rastreabilidade
de integrações**.

Não é, inicialmente:

- um TMS;
- uma gerenciadora de risco;
- uma solução de rastreamento veicular;
- um substituto para o sistema principal do cliente;
- um gateway obrigatório por onde todo o tráfego deve passar;
- uma ferramenta limitada ao mercado logístico.

O diferencial pretendido é unir monitoramento técnico detalhado com uma leitura
clara para operação, suporte, gestores e clientes.

## 5. Direções monitoradas

Os termos devem sempre ser interpretados da perspectiva do sistema do cliente.

### 5.1 Entrada — integração recebida

Um sistema externo chama uma API exposta pelo cliente.

Exemplo:

```text
Sistema externo -> API do cliente
```

O Pulso I/O precisa observar se a chamada chegou, quanto tempo levou, qual
resposta foi devolvida e se houve erro de processamento.

### 5.2 Saída — integração consumida

O sistema do cliente chama uma API ou serviço externo.

Exemplo:

```text
Sistema do cliente -> API externa
```

O Pulso I/O precisa observar tentativa, destino, duração, resposta, timeout,
falha de conexão, repetição e resultado final.

### 5.3 Bidirecional

Uma mesma relação entre sistemas pode ter operações de entrada e saída. Cada
operação será cadastrada e monitorada com sua direção, regra e histórico, mas
poderá ser agrupada em uma visão única da integração.

## 6. Estratégia de coleta

Não existe uma forma universal de conhecer o tráfego real sem instrumentar a
aplicação, receber eventos dela ou estar no caminho da comunicação. A solução
inicial será híbrida.

### 6.1 Pulso Agent

O **Pulso Agent** será a principal forma de coleta. Na primeira versão, será um
pacote para aplicações ASP.NET Core. Ele deverá usar instrumentação baseada em
OpenTelemetry para observar:

- requisições HTTP recebidas pelo ASP.NET Core;
- requisições HTTP realizadas por `HttpClient`;
- duração, resultado, exceções e contexto de rastreamento;
- propagação de `TraceId` e `CorrelationId`;
- atributos adicionais configurados pelo cliente.

Objetivo de experiência de instalação:

1. o administrador cadastra cliente, ambiente e integração no Pulso I/O;
2. o sistema gera uma chave exclusiva do ambiente;
3. o cliente instala o pacote do Agent;
4. adiciona uma configuração curta no `Program.cs`;
5. o Agent passa a enviar telemetria em segundo plano.

O Agent nunca deve bloquear ou derrubar a aplicação monitorada. O envio deve
ser assíncrono, em lote quando possível, com limites, tolerância a falhas e
descarte controlado quando a aplicação do Pulso estiver indisponível.

### 6.2 API de eventos

Sistemas que não puderem instalar o Agent poderão enviar um evento normalizado
para uma API de ingestão do Pulso I/O. Essa alternativa também será usada pelo
simulador durante o desenvolvimento.

### 6.3 Monitoramento ativo

O Pulso I/O poderá chamar periodicamente endpoints configurados para verificar:

- disponibilidade;
- latência;
- código HTTP;
- resposta mínima esperada;
- certificado e conectividade, futuramente.

O teste ativo não substitui a telemetria real. Uma API pode estar online e não
estar recebendo ou processando as mensagens de negócio esperadas.

### 6.4 Conectores futuros

Ficam fora do primeiro MVP:

- coleta em API gateways, proxies, IIS e Nginx;
- leitura de arquivos de log;
- conectores para filas, bancos, SFTP e mensageria;
- Agents específicos para Java, Node.js, Python e outras plataformas;
- recebimento OTLP público e genérico.

## 7. Níveis de observabilidade

| Nível | Pergunta respondida | Fonte principal |
| --- | --- | --- |
| Disponibilidade | O endpoint está acessível? | Monitoramento ativo |
| Fluxo | Os dados estão realmente entrando ou saindo? | Pulso Agent ou API de eventos |
| Transação | O que aconteceu com este registro específico? | Trace, correlação, log e validações |

Os dashboards devem deixar esses níveis separados. “Online” não é sinônimo de
“saudável”.

## 8. Contrato conceitual de telemetria

Cada evento de integração deverá conseguir representar, no mínimo:

- cliente e ambiente, derivados da credencial e não confiados cegamente ao payload;
- integração identificada;
- direção: entrada ou saída;
- data e hora em UTC;
- `TraceId`;
- `CorrelationId`;
- sistema de origem e sistema de destino;
- método e endpoint normalizado;
- status HTTP, quando aplicável;
- duração;
- sucesso ou falha;
- tipo e mensagem do erro;
- quantidade de tentativas, quando conhecida;
- tamanho aproximado da mensagem;
- chave de negócio opcional, como pedido, viagem ou documento;
- metadados permitidos;
- resultado das regras de saúde.

O cadastro da integração deverá permitir correspondência automática:

- entrada: método + rota da API do cliente;
- saída: método + host + rota da API externa;
- ambiente: produção, homologação ou desenvolvimento;
- sistema de origem e destino.

## 9. Segurança e privacidade

Princípios obrigatórios:

- não capturar payload completo por padrão;
- nunca armazenar `Authorization`, tokens, senhas ou segredos;
- permitir captura de conteúdo somente por configuração explícita;
- preferir captura de payload apenas em erros e com amostragem;
- mascarar campos sensíveis por lista configurável;
- limitar tamanho de corpo, log e mensagem de erro;
- criptografar dados sensíveis em trânsito e em repouso;
- definir retenção por cliente e tipo de dado;
- separar dados por `TenantId`;
- registrar auditoria de acessos e alterações administrativas;
- permitir detalhe técnico somente a usuários autorizados;
- observar LGPD desde o desenho, não apenas após o MVP.

## 10. Usuários e autorização

### Fase inicial

- usuário administrador inicial de Gustavo, criado por configuração segura e nunca fixo no código;
- administrador pode criar usuários por nome, e-mail e senha;
- login sempre realizado por e-mail e senha;
- senha com no mínimo 6 caracteres, incluindo maiúscula, minúscula e caractere especial;
- cada usuário possui estado ativo ou inativo;
- usuário inativo não pode iniciar nem manter uma sessão no sistema;
- cada usuário pertence a um cliente ou ao escopo global `Pulso I/O`;
- `ClientId` nulo representa o escopo raiz do Pulso I/O, com acesso a todos os clientes;
- usuários vinculados a um cliente só podem acessar dados daquele tenant;
- papel de autorização e escopo de cliente são conceitos separados;
- painel geral disponível a todo usuário autenticado e ativo;
- gestão de usuários restrita ao papel `Admin`.
- usuário autenticado pode gerenciar sua foto, e-mail e senha no próprio perfil;
- troca de e-mail ou senha exige a senha atual e invalida as sessões existentes.

### Evolução

- cada cliente acessa somente o próprio tenant;
- permissões futuras por perfil, como administrador, gestor, operação e técnico;
- restrição adicional por ambiente quando necessário;
- tela técnica e payloads protegidos por permissão específica.

## 11. Funcionalidades do MVP

### Administração

- login e logout;
- usuário administrador inicial;
- criação, listagem, ativação e desativação de usuários;
- pesquisa de usuários e vínculo da conta a um cliente ou ao escopo global;
- cadastro de clientes;
- cadastro de ambientes;
- geração e revogação de chaves de ingestão;
- cadastro de integrações;
- definição da direção da integração;
- configuração de regras de saúde.

### Monitoramento

- endpoint para ingestão de eventos;
- identificação da integração por regras de correspondência;
- histórico de execuções;
- estado atual de cada integração;
- monitoramento ativo básico;
- cálculo de saúde;
- visão de entrada, saída e bidirecional.

### Visualização

- dashboard geral;
- quantidade de integrações saudáveis, instáveis e indisponíveis;
- taxa de sucesso e falha;
- tempo médio e percentis de resposta, futuramente no MVP se necessário;
- última comunicação;
- filtros por cliente, ambiente, integração, direção e período;
- tela de detalhe de uma integração;
- tela de eventos, logs e debug técnico;
- indicação exata de erro, data, local e transação relacionada.

### Alertas

- alertas internos na aplicação no primeiro momento;
- e-mail, WhatsApp e outros canais ficam para fases posteriores.

## 12. Regras iniciais de saúde

Cada integração poderá definir:

- intervalo esperado entre mensagens;
- período máximo sem comunicação;
- tempo máximo de resposta;
- códigos HTTP considerados válidos;
- quantidade ou percentual tolerado de erros;
- quantidade mínima de eventos em uma janela;
- campos obrigatórios, quando houver validação de conteúdo;
- horários e dias em que a integração deve operar;
- quantidade máxima de tentativas;
- severidade por tipo de falha.

Estados iniciais sugeridos:

- **Saudável:** dentro de todas as regras principais;
- **Atenção:** degradação, atraso ou taxa de erro próxima do limite;
- **Crítico:** regra principal violada ou indisponibilidade confirmada;
- **Sem dados:** telemetria insuficiente para avaliar;
- **Pausado:** monitoramento suspenso intencionalmente.

## 13. Modelo de domínio inicial

Entidades principais:

- `User`
- `Tenant` ou `Client`
- `Environment`
- `Integration`
- `IntegrationEndpoint`
- `HealthRule`
- `IntegrationEvent`
- `IntegrationHealthSnapshot`
- `IngestionCredential`
- `Alert`
- `AuditLog`

Decisões de modelagem:

- todas as entidades do cliente devem possuir isolamento por tenant;
- o administrador inicial pertence ao escopo global Pulso I/O;
- ambientes são classificados inicialmente como produção, homologação ou desenvolvimento;
- integrações registram ambiente, direção, origem, destino e correspondência HTTP opcional;
- eventos devem ser imutáveis após a ingestão, salvo enriquecimento controlado;
- datas internas em UTC;
- rotas devem ser normalizadas para evitar cardinalidade excessiva;
- credenciais devem ser armazenadas por hash quando não precisarem ser recuperadas;
- logs administrativos e eventos de integração são conceitos diferentes.

## 14. Base técnica atual

- IDE: Visual Studio Code
- Backend: C# com ASP.NET Core Web API
- Runtime: .NET 10 LTS
- Frontend: Angular 22, TypeScript e SCSS
- Banco principal: PostgreSQL
- ORM: Entity Framework Core com provider Npgsql
- Autenticação: ASP.NET Core Identity + JWT e refresh token
- Documentação da API: OpenAPI/Swagger
- Desenvolvimento local principal: execução direta com .NET, Node.js e PostgreSQL
  instalado como serviço do Windows
- Docker Compose: alternativa mantida no repositório para uso futuro, sem prazo,
  pois a máquina atual não disponibiliza virtualização de hardware ao Docker Desktop
- Testes backend: xUnit
- Testes frontend: Vitest
- Telemetria do Agent: OpenTelemetry
- Arquitetura: monólito modular
- Versionamento: Git em repositório privado

Não adicionar microserviços, Kubernetes, Kafka, RabbitMQ, Redis ou mecanismos
distribuídos sem uma necessidade comprovada do produto.

## 15. Estrutura planejada do repositório

```text
pulso-io/
├── src/
│   ├── backend/
│   │   ├── PulsoIO.Api/
│   │   ├── PulsoIO.Application/
│   │   ├── PulsoIO.Domain/
│   │   └── PulsoIO.Infrastructure/
│   ├── frontend/
│   │   └── pulso-io-web/
│   └── agent/
│       └── PulsoIO.Agent.AspNetCore/
├── tests/
│   ├── PulsoIO.UnitTests/
│   └── PulsoIO.IntegrationTests/
├── simulator/
│   └── PulsoIO.Simulator/
├── docs/
│   └── product-context.md
├── compose.yaml
└── PulsoIO.sln
```

## 16. Estratégia de implementação

### Marco 0 — Ambiente

- validar .NET, Node.js, npm, Angular CLI e Git;
- manter Docker como ferramenta opcional enquanto a virtualização não estiver disponível;
- criar o repositório privado;
- criar a estrutura base.

### Marco 1 — Projeto executável

- criar solução .NET e aplicação Angular;
- executar PostgreSQL como serviço nativo do Windows;
- preservar o Docker Compose como alternativa futura e para outros ambientes;
- disponibilizar `/health` e Swagger;
- exibir no Angular o estado da API.

### Marco 2 — Administração

- autenticação do administrador;
- gestão de usuários ativos e inativos;
- clientes;
- ambientes;
- integrações;
- regras básicas de saúde;
- credenciais de ingestão.

### Marco 3 — Primeiro fluxo real

- criar contrato de evento versionado;
- criar endpoint de ingestão;
- validar autenticação e limites;
- persistir eventos;
- criar simulador;
- listar eventos no frontend.

### Marco 4 — Saúde e diagnóstico

- processar regras;
- calcular estado atual;
- construir dashboard;
- criar tela de detalhe e debug;
- implementar alertas internos.

### Marco 5 — Pulso Agent

- criar pacote ASP.NET Core;
- instrumentar entrada e saída;
- mapear spans para o contrato do Pulso;
- enviar em segundo plano;
- testar falhas, indisponibilidade e volume;
- documentar instalação.

### Marco 6 — Piloto

- instalar somente após testes com o simulador;
- começar em homologação no sistema parceiro;
- monitorar poucas integrações bem conhecidas;
- validar valor operacional antes de ampliar;
- não capturar conteúdo sensível sem aprovação e configuração.

## 17. Validação comercial existente

- A ideia nasceu de uma dor real observada profissionalmente.
- Desenvolvedores e profissionais de operação consultados reagiram positivamente.
- Existe um contato com um sistema em crescimento disposto a servir como piloto.
- Esse contato também demonstrou interesse em oferecer o Pulso I/O junto ao próprio produto.
- Dados técnicos e comerciais do parceiro serão coletados somente quando o produto estiver preparado para o piloto.

## 18. Fora do escopo inicial

- inteligência artificial para explicar erros;
- gateway obrigatório de integrações;
- substituição de ferramentas de negócio;
- aplicativo mobile;
- billing e cobrança automática;
- múltiplos canais de alerta;
- observabilidade completa de infraestrutura;
- suporte universal a todas as linguagens;
- armazenamento indiscriminado de payload;
- implantação direta em produção do parceiro antes de testes locais e em homologação.

## 19. Decisões ainda pendentes

- domínio oficial e disponibilidade dos identificadores;
- resultado formal da busca e pedido de marca no INPI;
- códigos hexadecimais e tokens finais da paleta;
- arquivo vetorial definitivo do logo;
- provedor de hospedagem;
- política comercial, planos e limites de eventos;
- período padrão de retenção;
- modelo definitivo de classificação de erros;
- formato versionado do contrato `IntegrationEvent`;
- estratégia definitiva entre exportador próprio e endpoint OTLP;
- biblioteca visual do Angular;
- canal inicial de alertas externos;
- termos de uso, política de privacidade e contrato do piloto.

## 20. Estado atual do projeto

- Nome escolhido: Pulso I/O.
- Direção visual conceitual escolhida.
- Problema, público e proposta definidos.
- Monitoramento de entrada e saída definido como requisito central.
- Pulso Agent escolhido como principal estratégia de coleta.
- Stack e arquitetura inicial propostas.
- Estrutura inicial criada no repositório local como monólito modular.
- Backend .NET 10 e frontend Angular 22 compilam com sucesso.
- Node.js e dependências do Angular estão instalados e validados.
- Docker Desktop está indisponível porque a virtualização de hardware permanece
  desabilitada no BIOS, sem previsão para atualização do firmware.
- Decisão vigente: desenvolver localmente sem Docker, usando PostgreSQL nativo no Windows.
- PostgreSQL 18.4 está instalado como serviço do Windows, com banco e usuário
  próprios para o Pulso I/O e conexão armazenada via .NET User Secrets.
- As migrations inicial, de autenticação e de status ativo do módulo Identity foram criadas
  e aplicadas com sucesso ao PostgreSQL local.
- API, acesso ao PostgreSQL e build do frontend foram validados em execução local.
- Marco 1 concluído: o frontend consulta e exibe o estado real da API, utiliza a
  identidade visual oficial e encaminha chamadas locais pelo proxy do Angular.
- OpenAPI 3.1 e Swagger UI foram restaurados e ficam disponíveis apenas em Development.
- Builds, acesso ao PostgreSQL, integração frontend/backend e renderização responsiva
  foram validados localmente sem Docker.
- A primeira etapa do Marco 2 foi implementada com ASP.NET Core Identity, papel `Admin`,
  JWT de 15 minutos e refresh token rotativo em cookie `HttpOnly`, com expiração absoluta
  de sete dias por família de sessão.
- Refresh tokens são persistidos somente por hash, agrupados por família e revogados em
  caso de reutilização; login, refresh e logout exigem proteção CSRF e origem autorizada.
- O access token do Angular permanece apenas em memória, com renovação coordenada entre
  abas; não é armazenado em `localStorage`, `sessionStorage` ou `IndexedDB`.
- O frontend possui rotas para landing page, login e painel autenticado, sem alterar a
  direção visual aprovada; a gestão de usuários aparece somente para administradores.
- O login utiliza e-mail e senha com mínimo de 6 caracteres, ao menos uma maiúscula, uma
  minúscula e um caractere especial; números são opcionais.
- Administradores podem listar, criar, ativar e desativar usuários. Contas novas são comuns
  por padrão e não recebem automaticamente o papel `Admin`.
- Administradores raiz podem cadastrar clientes, ambientes e integrações; administradores
  vinculados permanecem limitados ao próprio cliente, inclusive ao criar novas contas.
- O dashboard autenticado apresenta totais reais de clientes, ambientes e integrações no
  escopo do usuário, além da disponibilidade do processo da API. Estados de saúde continuam
  sem dados até a implementação da ingestão de telemetria.
- A desativação invalida imediatamente JWT e todos os refresh tokens do usuário; login,
  refresh e requisições autenticadas rejeitam contas inativas com resposta genérica.
- Autodesativação, desativação do último administrador ativo e atualizações concorrentes de
  status são protegidas no backend.
- O bootstrap do primeiro administrador utiliza exclusivamente .NET User Secrets e é
  removível após a criação idempotente da conta; nenhuma credencial foi fixada no código.
- A chave JWT local foi gerada criptograficamente e armazenada no User Secrets.
- O Docker Compose futuro passou a exigir segredos externos em `.env`, sem senhas fixas no
  arquivo versionado.
- O backend possui 26 testes xUnit para emissão de tokens, hash, security stamp, lockout,
  status de usuário e concorrência; o frontend possui 20 testes Vitest em 6 arquivos para
  sessão, interceptor, guards, rotas, política de senha e gestão de usuários.
- Build Release, build Angular, auditorias de dependências e smoke tests HTTP da
  autenticação foram validados sem erros ou vulnerabilidades de produção conhecidas.
- Próxima ação: configurar a credencial do administrador por entrada segura no terminal,
  validar o fluxo completo de login e gestão de usuários e então iniciar o cadastro de
  clientes do Marco 2.

## 21. Regras para quem trabalhar no código

1. Ler este documento antes de alterar o produto.
2. Não limitar nomes, regras ou telas à logística.
3. Tratar entrada e saída como recursos de primeira classe.
4. Manter isolamento multi-tenant desde o modelo de dados.
5. Não registrar segredos ou payloads sensíveis.
6. Preferir soluções simples e testáveis para o MVP.
7. Não introduzir infraestrutura distribuída sem necessidade demonstrada.
8. Criar migrations para alterações de banco.
9. Adicionar testes para regras de saúde e isolamento de tenant.
10. Atualizar este documento quando uma decisão funcional ou arquitetural mudar.

## 22. Referências técnicas

- [.NET e OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Instrumentação OpenTelemetry para .NET](https://opentelemetry.io/docs/languages/dotnet/libraries/)
- [Protocolo OTLP](https://opentelemetry.io/docs/specs/otel/protocol/)
- [Política de suporte do .NET](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- [Versões e suporte do Angular](https://angular.dev/reference/releases)
