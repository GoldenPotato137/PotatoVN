#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

curl --fail --location --retry 3 --retry-all-errors \
  --output vn_mapper.db \
  https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db
curl --fail --location --retry 3 --retry-all-errors \
  --output vndb-tags-latest.json.gz \
  https://dl.vndb.org/dump/vndb-tags-latest.json.gz
gzip --force --decompress vndb-tags-latest.json.gz
