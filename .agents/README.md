# Agentes de Assistencia - TACE

Pasta de definicoes de agentes de IA para o projeto **TACE**. Cada arquivo e um
**prompt de sistema completo**, pronto para ser usado como Custom GPT (ChatGPT),
como `AGENTS.md` de um subagente ou como instrucao de sistema de outro agente.

## Agentes disponiveis

| Arquivo | Agente | Foco principal |
|---|---|---|
| `dotnet-architect.md` | **.NET Architect** | Clean Architecture, DDD, CQRS; planeja bounded contexts, agregados, contratos, contextos do EF Core, endpoints e DI. Nao escreve codigo pronto: entrega plano estrutural. |
| `csharp-dotnet-senior-developer.md` | **C# & .NET Senior Developer** | Diretrizes oficiais de design da Microsoft; refatoracao de legado para C# 12/13/14 e .NET 8/9/10; async/await, nullable, performance, seguranca e testes. |
| `AGENTS.md` | **Diretrizes do repositorio** | Regras globais do TACE (Ouro #1 a #4) que qualquer agente deve respeitar. |

## Como usar

### 1. Como Custom GPT (ChatGPT)
1. Abra "Create GPT" > **Configure**.
2. Cole o conteudo integral do arquivo desejado no campo **Instructions**.
3. Em **Knowledge**, anexe os arquivos do repositorio que o agente precisa ler
   (por exemplo, `memoria_TACE.md` e os `.cs`/`.c` em foco).
4. Salve e teste com um pedido real do projeto.

### 2. Como subagente / instrucao local
- Copie o conteudo do arquivo para um prompt de sistema do subagente, ou
  referencie o caminho do arquivo na instrucao ("Leia e siga `.agents/<arquivo>.md`").

### 3. Como checklist de code review
Use a secao de formato de saida do agente correspondente: ele define a ordem e a
severidade dos apontamentos esperados.

## Regras comuns a todos os agentes deste repositorio

1. **Zero warnings** - build sem erros e sem avisos.
2. **Isolamento por modelo** - alteracoes em `src/core/functions/<modelo>/` nunca
   afetam outro equipamento (Vision 7, TabMulti M7, Nokia T20).
3. **Rastreabilidade** - toda mudanca registrada em `memoria_TACE.md` e commit no
   padrao Conventional Commits.
4. **Build somente com ordem explicita** do usuario.
5. **Zero poluicao** - nada de binarios ou arquivos soltos na raiz do repositorio.
