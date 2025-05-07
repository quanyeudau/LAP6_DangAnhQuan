using ASC.Business.Interfaces;
using ASC.DataAccess;
using ASC.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Business
{
    public class MasterDataOperations : IMasterDataOperations
    {
        private readonly IUnitOfWork _unitOfWork;

        public MasterDataOperations(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<MasterDataKey>> GetAllMasterKeysAsync()
        {
            var masterKeys = await _unitOfWork.Repository<MasterDataKey>().FindAllAsync();
            return masterKeys.ToList();
        }

        public async Task<List<MasterDataKey>> GetMasterKeyByNameAsync(string name)
        {
            var masterKeys = await _unitOfWork.Repository<MasterDataKey>().FindAllByPartitionKeyAsync(name);
            return masterKeys.ToList();
        }

        public async Task<bool> InsertMasterKeyAsync(MasterDataKey key)
        {
            try
            {
                await _unitOfWork.Repository<MasterDataKey>().AddAsync(key);
                var result = await _unitOfWork.SaveChangesAsync();
                if (result > 0)
                {
                    Console.WriteLine("MasterDataKey added successfully.");
                    return true;
                }
                else
                {
                    Console.WriteLine("No changes were saved to the database.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting MasterDataKey: {ex.Message}");
                return false;
            }
        }


        public async Task<bool> UpdateMasterKeyAsync(string originalPartitionKey, MasterDataKey key)
        {
            var masterKey = await _unitOfWork.Repository<MasterDataKey>().FindAsync(originalPartitionKey, key.RowKey);
            if (masterKey != null)
            {
                masterKey.IsActive = key.IsActive;
                masterKey.IsDeleted = key.IsDeleted;
                masterKey.Name = key.Name;

                _unitOfWork.Repository<MasterDataKey>().Update(masterKey);
                await _unitOfWork.SaveChangesAsync();
            }
            return true;
        }

        public async Task<List<MasterDataValue>> GetAllMasterValuesByKeyAsync(string key)
        {
            try
            {
                var masterValues = await _unitOfWork.Repository<MasterDataValue>().FindAllByPartitionKeyAsync(key);
                return masterValues.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public async Task<MasterDataValue> GetMasterValueByNameAsync(string key, string name)
        {
            var masterValue = await _unitOfWork.Repository<MasterDataValue>().FindAsync(key, name);
            return masterValue;
        }

        public async Task<bool> InsertMasterValueAsync(MasterDataValue value)
        {
            await _unitOfWork.Repository<MasterDataValue>().AddAsync(value);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateMasterValueAsync(string originalPartitionKey, string originalRowKey, MasterDataValue value)
        {
            var masterValue = await _unitOfWork.Repository<MasterDataValue>().FindAsync(originalPartitionKey, originalRowKey);
            if (masterValue != null)
            {
                masterValue.IsActive = value.IsActive;
                masterValue.IsDeleted = value.IsDeleted;
                masterValue.Name = value.Name;

                _unitOfWork.Repository<MasterDataValue>().Update(masterValue);
                await _unitOfWork.SaveChangesAsync();
            }
            return true;
        }

        public async Task<List<MasterDataValue>> GetAllMasterValuesAsync()
        {
            var masterValues = await _unitOfWork.Repository<MasterDataValue>().FindAllAsync();
            return masterValues.ToList();
        }

        public async Task<bool> UploadBulkMasterData(List<MasterDataValue> values)
        {
            foreach (var value in values)
            {
                var masterKey = await GetMasterKeyByNameAsync(value.PartitionKey);
                if (masterKey == null || !masterKey.Any())
                {
                    await _unitOfWork.Repository<MasterDataKey>().AddAsync(new MasterDataKey()
                    {
                        Name = value.PartitionKey,
                        RowKey = Guid.NewGuid().ToString(),
                        PartitionKey = value.PartitionKey,
                        CreatedBy = "System",
                        UpdatedBy = "System"
                    });
                }

                var masterValuesByKey = await GetAllMasterValuesByKeyAsync(value.PartitionKey);
                var existingValue = masterValuesByKey?.FirstOrDefault(p => p.Name == value.Name);

                if (existingValue == null)
                {
                    await _unitOfWork.Repository<MasterDataValue>().AddAsync(value);
                }
                else
                {
                    existingValue.IsActive = value.IsActive;
                    existingValue.IsDeleted = value.IsDeleted;
                    existingValue.Name = value.Name;

                    _unitOfWork.Repository<MasterDataValue>().Update(existingValue);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
