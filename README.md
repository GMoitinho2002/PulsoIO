# Pulso I/O

Monólito modular com ASP.NET Core 10, Angular 22, PostgreSQL e Entity Framework Core.

- `src/PulsoIO.Api`: host e composition root;
- `src/Modules`: módulos de negócio;
- `src/BuildingBlocks`: abstrações compartilhadas;
- `src/Web`: aplicação Angular;
- `tests`: testes automatizados do backend.

## Desenvolvimento local sem Docker

Pré-requisitos: .NET 10, Node.js 24 e PostgreSQL 18 instalado como serviço do Windows.
O Docker permanece apenas como alternativa futura enquanto a virtualização da máquina não
estiver disponível.

### Segredos da API

A conexão PostgreSQL deve ficar no .NET User Secrets, na chave
`ConnectionStrings:Database`. Senhas e chaves nunca devem ser adicionadas aos arquivos
versionados.

Em uma instalação nova, gere também uma chave JWT aleatória:

```powershell
$secretProject = ".\src\PulsoIO.Api\PulsoIO.Api.csproj"
$jwtBytes = New-Object byte[] 64
$jwtGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()

try {
  $jwtGenerator.GetBytes($jwtBytes)
  $jwtSigningKey = [Convert]::ToBase64String($jwtBytes)
  dotnet user-secrets set "Authentication:Jwt:SigningKey" $jwtSigningKey --project $secretProject
}
finally {
  $jwtGenerator.Dispose()
  Remove-Variable jwtBytes, jwtSigningKey -ErrorAction SilentlyContinue
}
```

### Migrations

Restaure a ferramenta local após clonar o repositório:

```powershell
dotnet tool restore
```

Para aplicar as migrations do módulo Identity:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet tool run dotnet-ef database update `
  --project .\src\Modules\Identity\PulsoIO.Modules.Identity\PulsoIO.Modules.Identity.csproj `
  --startup-project .\src\PulsoIO.Api\PulsoIO.Api.csproj `
  --context IdentityDbContext
```

### Administrador inicial

Configure nome, e-mail e senha sem exibir a senha no terminal:

```powershell
$secretProject = ".\src\PulsoIO.Api\PulsoIO.Api.csproj"
$adminName = Read-Host "Nome do administrador"
$adminEmail = Read-Host "E-mail do administrador"
$adminPassword = Read-Host "Senha do administrador" -AsSecureString
$adminCredential = [pscredential]::new($adminEmail, $adminPassword)
$adminPasswordText = $adminCredential.GetNetworkCredential().Password

try {
  dotnet user-secrets set "Authentication:InitialAdmin:Name" $adminName --project $secretProject
  dotnet user-secrets set "Authentication:InitialAdmin:Email" $adminEmail --project $secretProject
  dotnet user-secrets set "Authentication:InitialAdmin:Password" $adminPasswordText --project $secretProject
}
finally {
  Remove-Variable adminName, adminEmail, adminPassword, adminCredential, adminPasswordText
}
```

A senha deve ter ao menos 6 caracteres e combinar ao menos uma letra maiúscula, uma letra
minúscula e um caractere especial. Números são aceitos, mas não são obrigatórios. Inicie a
API uma vez e aguarde o log
`Administrador inicial garantido`. Depois, remova os três segredos de bootstrap; a conta e a
senha persistem no banco:

```powershell
$secretProject = ".\src\PulsoIO.Api\PulsoIO.Api.csproj"
dotnet user-secrets remove "Authentication:InitialAdmin:Name" --project $secretProject
dotnet user-secrets remove "Authentication:InitialAdmin:Email" --project $secretProject
dotnet user-secrets remove "Authentication:InitialAdmin:Password" --project $secretProject
```

### Executar

Na raiz do projeto, inicie a API:

```powershell
dotnet run --project .\src\PulsoIO.Api
```

Em outro terminal, inicie o frontend:

```powershell
npm.cmd --prefix .\src\Web start
```

Endereços locais:

- aplicação: `http://localhost:4200`;
- login: `http://localhost:4200/login`;
- painel autenticado: `http://localhost:4200/app`;
- gestão de usuários, exclusiva para administradores: `http://localhost:4200/app/users`;
- estado da API: `http://localhost:5143/health`;
- Swagger UI: `http://localhost:5143/swagger`;
- OpenAPI: `http://localhost:5143/openapi/v1.json`.

O OpenAPI e o Swagger ficam disponíveis somente em `Development`. Durante o
desenvolvimento, o Angular encaminha `/health`, `/api`, `/openapi` e `/swagger` para a API
local.

O login utiliza e-mail e senha. Administradores podem criar contas e ativá-las ou desativá-las
na opção **Usuários** do menu. Uma conta desativada perde o acesso imediatamente, inclusive
em sessões já abertas. Contas criadas nessa tela são usuários comuns por padrão e não recebem
automaticamente o papel `Admin`.

## Validação

```powershell
dotnet build .\PulsoIO.slnx -c Release
dotnet test .\PulsoIO.slnx -c Release --no-build
npm.cmd --prefix .\src\Web test
npm.cmd --prefix .\src\Web run build
npm.cmd --prefix .\src\Web audit --omit=dev
```

## Docker (alternativa futura)

Quando houver virtualização disponível, copie `.env.example` para `.env`, preencha todos os
segredos locais e execute:

```powershell
docker compose up --build
```
