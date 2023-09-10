# MetricasRabbitMQ

Execute o seguinte comando no mesmo diretório que contém seu arquivo docker-compose.yml:
docker-compose up

Será possível então enviar métricas para localhost:8125 via UDP. Como foi configurado o Telegraf para imprimir métricas no stdout, será possível ver as métricas no terminal onde você executou o docker-compose up. Assim temos um servidor StatsD rodando localmente via Telegraf, e você deve ser capaz de enviar métricas para ele.



