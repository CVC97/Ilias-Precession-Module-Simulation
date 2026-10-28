// anything that runs a GyroscopeModel (GyroscopeController of the original, ModuleGyroscopeController of the module);
// views like VectorDisplay only need the model, not the controller behind it
public interface IGyroscopeModelSource {
    GyroscopeModel Model { get; }
}
