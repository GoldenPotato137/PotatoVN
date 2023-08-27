cd "$(dirname "$0")" || exit

wget https://github.com/GoldenPotato137/PotatoDBMapper/raw/main/assets/db/vn_mapper.db -O vn_mapper.db
wget https://dl.vndb.org/dump/vndb-tags-latest.json.gz -O vndb-tags-latest.json.gz
gzip -d -f vndb-tags-latest.json.gz
