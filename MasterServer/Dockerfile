FROM python:3.12-slim
WORKDIR /app
COPY server.py svg_merge.py merge_plan.py ./
RUN useradd --uid 10001 --create-home masterserver && mkdir /data && chown masterserver /data
USER masterserver
EXPOSE 8090
VOLUME ["/data"]
ENTRYPOINT ["python", "server.py", "--host", "0.0.0.0", "--config", "/config/config.json", "--database", "/data/repository.sqlite3"]
