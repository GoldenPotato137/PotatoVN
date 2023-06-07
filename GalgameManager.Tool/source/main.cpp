#include <memory>

#include "converter.h"
#include "filters/value_filter.h"

using namespace std;

int main(int argc, char* argv[])
{
    const converter converter("result", "data", "data");

    // producers
    vector<unique_ptr<filter>> filters;
    filters.emplace_back(make_unique<value_filter>("type", "co"));
    converter.convert_vndb_to_json("producers.header", "producers", "producers.json",filters);

    return 0;
}
