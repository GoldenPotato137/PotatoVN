#pragma once
#include <memory>
#include <string>
#include <vector>
#include <vector>

#include "filters/filter.h"

class converter
{
public:
    /**
     * \param output_path 输出路径
     * \param header_path header文件路径
     * \param data_path  data文件路径
     */
    converter(std::string&& output_path, std::string&& header_path, std::string&& data_path);
    
    /**
     * \brief 将vndb的数据导出转换为json格式
     * \param header_file header文件名字
     * \param data_file 数据文件名字
     * \param output_file 输出文件名字
     * \param filters 过滤器
     */
    void convert_vndb_to_json(const std::string& header_file, const std::string& data_file, const std::string&
                              output_file, const std::vector<std::unique_ptr<filter>>& filters) const;

private:
    std::string output_path_;
    std::string header_path_;
    std::string data_path_;

    std::vector<std::string> static split(const std::string& str, char separator);
};
