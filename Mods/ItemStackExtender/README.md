# Item Stack Extender

Aumenta o limite de itens empilháveis no servidor e no cliente.

Configuração padrão:

- `MaxStack`: `100000`
- `PreserveSingleItems`: mantém armas, armaduras, acessórios e outros itens
  originalmente limitados a uma unidade.

A versão 0.2 sincroniza diretamente `MaxStackCount` nos dados carregados e
mantém o hook como proteção adicional.

O plugin deve usar a mesma configuração no servidor e em todos os clientes.
