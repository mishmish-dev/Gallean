using TorchSharp;
using TorchSharp.Modules;
using TorchSharp.Utils;
using static TorchSharp.torch.nn;

namespace Umbra.Ltx;

internal sealed class LtxGeluProjection : Module<torch.Tensor, torch.Tensor>
{
    [ComponentName(Name = "proj")]
    private readonly Linear projection;
    private readonly Module<torch.Tensor, torch.Tensor> activation;

    public LtxGeluProjection(int inputChannels, int outputChannels, bool hasBias)
        : base(nameof(LtxGeluProjection))
    {
        projection = Linear(inputChannels, outputChannels, hasBias);
        activation = new TanhGelu();

        RegisterComponents();
    }

    public override torch.Tensor forward(torch.Tensor input)
    {
        using IDisposable scope = torch.NewDisposeScope();

        using torch.Tensor hidden = projection.forward(input);

        return activation.forward(hidden).MoveToOuterDisposeScope();
    }
}
