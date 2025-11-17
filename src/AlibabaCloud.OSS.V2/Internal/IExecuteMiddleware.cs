using System.Threading.Tasks;


namespace AlibabaCloud.OSS.V2.Internal
{
    internal interface IExecuteMiddleware
    {
        public Task<ResponseMessage> ExecuteAsync(RequestMessage request, ExecuteContext context);
        
        public ResponseMessage Execute(RequestMessage request, ExecuteContext context);
    }
}
