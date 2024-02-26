using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Repositories;

public class OssRecordRepository(DataContext dataContext) : IOssRecordRepository
{
    public async Task<OssRecord?> GetRecordByKeyAsync(string key)
    {
        return await dataContext.OssRecords.FindAsync(key);
    }

    public async Task<long> UpdateRecordAsync(int userId, string key, long size)
    {
        OssRecord? record = await GetRecordByKeyAsync(key);
        if (size == 0)
        {
            if(record is not null) dataContext.OssRecords.Remove(record);
            await dataContext.SaveChangesAsync();
            return -record?.Size ?? 0;
        }

        if (record is null)
        {
            record = new OssRecord
            {
                Key = key,
                Size = size,
                UserId = userId
            };
            dataContext.OssRecords.Add(record);
            await dataContext.SaveChangesAsync();
            return size;
        }

        var result = size - record.Size;
        record.Size = size;
        dataContext.OssRecords.Update(record);
        await dataContext.SaveChangesAsync();
        return result;
    }
}