#pragma once
#include <string>

class filter
{
public:
    virtual ~filter() = default;
    bool virtual apply(const std::string& key,const std::string& value) const = 0;
};
