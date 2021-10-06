FROM otel/opentelemetry-collector-contrib

COPY ./otel_collector_config.yaml /etc/otel/config.yaml 
 
EXPOSE 55680 55681

