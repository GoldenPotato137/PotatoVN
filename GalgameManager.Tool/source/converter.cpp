#include "converter.h"

#include <fstream>
#include <regex>
#include <sstream>

using namespace std;

converter::converter(std::string&& output_path, std::string&& header_path, std::string&& data_path)
{
    this->output_path_ = output_path;
    this->header_path_ = header_path;
    this->data_path_ = data_path;
}

void converter::convert_vndb_to_json(const string& header_file, const string& data_file, const string& output_file,
                                     const std::vector<std::unique_ptr<filter>>& filters) const
{
    ifstream header_stream;
    header_stream.open(header_path_ + "\\" + header_file, ios::in);
    if (!header_stream.is_open())
        throw exception("header file not found");
    string str;
    getline(header_stream, str);
    vector<string> header = split(str, '\t');
    header_stream.close();

    ifstream data_stream;
    data_stream.open(data_path_ + "\\" + data_file, ios::in);
    if (!data_stream.is_open())
        throw exception("data file not found");
    ofstream output_stream;
    output_stream.open(output_path_ + "\\" + output_file, ios::out | ios::trunc);
    if (!output_stream.is_open())
        throw exception("cannot open output file");

    output_stream << "[";
    regex reg("\"");
    bool first = true;
    while (getline(data_stream, str))
    {
        vector<string> data = split(str, '\t');
        // 确认过滤之后是否还要插入这个数据
        bool found = false;
        for (unsigned int i = 0; i < header.size() && found == false; i++)
            for (const auto& filter : filters)
                if (filter->apply(header[i], data[i]))
                {
                    found = true;
                    break;
                }
        if (found == false) continue;

        if (!first) output_stream << ","; // 补上上个数据的逗号
        first = false;
        output_stream << "{";
        bool t_first = true;
        for (unsigned int i = 0; i < header.size(); i++)
        {
            if (!t_first) output_stream << ",";
            t_first = false;
            if (data[i] == "\\N") data[i] = "null";
            data[i] = regex_replace(data[i], reg, "\\\"");
            output_stream << "\"" << header[i] << "\": \"" << data[i] << "\"";
        }
        output_stream << "}";
    }
    output_stream << "]";

    output_stream.close();
    data_stream.close();
}

std::vector<std::string> converter::split(const std::string& str, char separator)
{
    vector<string> result;
    istringstream iss(str);
    string tmp;
    while (std::getline(iss, tmp, separator))
    {
        result.emplace_back(tmp);
    }

    // 如果最后一个字符是分隔符，那么最后一个字符串为空
    if (str[str.size() - 1] == separator)
        result.emplace_back("");
    return result;
}
