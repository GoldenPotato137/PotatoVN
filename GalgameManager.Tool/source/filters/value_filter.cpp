#include "value_filter.h"

value_filter::value_filter(std::string&& key, std::string&& value)
{
    key_ = std::move(key);
    value_ = std::move(value);
}

bool value_filter::apply(const std::string& key, const std::string& value) const
{
    if(value_ == "*")
        return key == key_;
    return key == key_ && value == value_;
}
