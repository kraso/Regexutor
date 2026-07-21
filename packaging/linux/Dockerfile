# docker build -f packaging/linux/Dockerfile -t regexutor-deb .
# docker run --rm -v "%cd%/publish:/out" regexutor-deb sh -c "cp /artifacts/*.deb /out/"
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends dpkg-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
RUN sed -i 's/\r$//' packaging/linux/build-deb.sh \
    && mkdir -p /artifacts \
    && OUT=/artifacts bash packaging/linux/build-deb.sh \
    && ls -la /artifacts
