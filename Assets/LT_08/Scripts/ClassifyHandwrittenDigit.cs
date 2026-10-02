using UnityEngine;
using Unity.InferenceEngine;

public class ClassifyHandwrittenDigit : MonoBehaviour
{
    public Texture2D inputTexture;
    public ModelAsset modelAsset;

    Model runtimeModel;
    Worker worker;
    public float[] results;

    void Start()
    {
        Model sourceModel = ModelLoader.Load(modelAsset);

        // Create a functional graph that runs the input model and then applies softmax to the output.
        FunctionalGraph graph = new FunctionalGraph();
        FunctionalTensor[] inputs = graph.AddInputs(sourceModel);
        FunctionalTensor[] outputs = Functional.Forward(sourceModel, inputs);
        FunctionalTensor softmax = Functional.Softmax(outputs[0]);

        // Create a model with softmax by compiling the functional graph.
        graph.AddOutput(softmax);
        runtimeModel = graph.Compile();

        // Create input data as a tensor
        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 1, 28, 28));
        TextureConverter.ToTensor(inputTexture, inputTensor);

        // Create an engine
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        // Run the model with the input data
        worker.Schedule(inputTensor);

        // Get the result
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        // outputTensor is still pending
        // Either read back the results asynchronously or do a blocking download call
        results = outputTensor.DownloadToArray();

        // Release outputTensor memory
        outputTensor.Dispose();
    }

    void OnDisable()
    {
        // Tell the GPU we're finished with the memory the engine used
        worker.Dispose();
    }
}
