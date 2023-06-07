#pragma once
#include "filter.h"

class value_filter final : public filter
{
public:
    value_filter(std::string&& key, std::string&& value);
    bool apply(const std::string& key, const std::string& value) const override;

private:
    std::string key_, value_;
};
