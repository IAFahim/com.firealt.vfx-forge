// Note: these methods are required for the VFX Forge Demo Sample as for some reason, upon importing the Sample, 
// the custom connections with Sample types VFXDamageNumberDataDEMO and VFXDamageNumberArrayDataDEMO would always break. 
// In your actual VFX Assets use "Sample Graphics Buffer" node instead

void SampleDamageNumberDataDEMO(
    uint index, StructuredBuffer<VFXDamageNumberDataDEMO> buffer,
    out float3 position,
    out uint type
)
{
    VFXDamageNumberDataDEMO data = buffer[index];

    position = data.Position;
    type = data.Type;
}


void SampleDamageNumberArrayDataDEMO(
    uint index, StructuredBuffer<VFXDamageNumberArrayDataDEMO> ArrayDataBuffer,
    out uint glyphIndex
)
{
    VFXDamageNumberArrayDataDEMO data = ArrayDataBuffer[index];

    glyphIndex = data.GlyphIndex;
}