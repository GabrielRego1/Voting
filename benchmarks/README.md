# Benchmarks de capacidade

Evidências versionadas do teste de limite (stress) da API de votação, uma pasta por versão do sistema, para comparar a evolução entre versões.

| Versão | Capacidade sustentável | Ruptura | Ambiente | Relatório |
|---|---:|---|---|---|
| v0 (`6218bfa`) | 15.000 votos/s | entre 20.000 e 21.000 votos/s | notebook na bateria (`power-saver`) | [relatorio-v0.md](v0/relatorio-v0.md) |

## Estrutura

```text
benchmarks/
└── <versão>/
    ├── relatorio-<versão>.md   # relatório final, escrito e revisado (leitura oficial)
    ├── README.md               # resumo gerado por tools/stress/summarize.py
    ├── summary.json            # dados consolidados (schemaVersion 1), usados pelo compare.py
    └── raw/
        ├── environment.json    # commit, máquina, energia, split de CPUs, parâmetros
        ├── steps.jsonl         # veredito de cada degrau
        ├── <degrau>.json       # relatório do gerador (contracts/workload-cli.md)
        ├── <degrau>.log        # progresso do gerador (stderr)
        ├── <degrau>.resources.csv  # CPU/memória da API, PostgreSQL e gerador a cada 1 s
        ├── api.log, build.log
        └── extra/              # experimentos manuais citados no relatório
```

## Como gerar uma nova versão

```bash
make stress BENCH_VERSION=v1          # ATENÇÃO: recria o banco local (docker compose down -v)
python3 tools/stress/compare.py benchmarks/v0 benchmarks/v1
```

- Rode na tomada, com `powerprofilesctl set performance`. O script avisa se estiver na bateria e registra o estado de energia.
- Mantenha o mesmo split de CPUs (`API_CPUS`, `PG_CPUS`, `GEN_CPUS`) e os mesmos parâmetros para que a comparação seja justa. O `compare.py` avisa quando eles divergem.
- Depois da execução, escreva o `relatorio-<versão>.md` com a análise e atualize a tabela acima.
