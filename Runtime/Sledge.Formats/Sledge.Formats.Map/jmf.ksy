meta:
  id: jmf
  file-extension: jmf
  endian: le
seq:
  - id: header
    type: header
  - id: entities
    type: entity
    repeat: eos
types:
  header:
    seq:
      - id: magic
        contents: 'JHMF'
      - id: version
        type: s4
      - id: something
        type: s4
      - id: num_groups
        type: s4
      - id: groups
        type: group
        repeat: expr
        repeat-expr: num_groups
      - id: num_visgroups
        type: s4
      - id: visgroups
        type: visgroup
        repeat: expr
        repeat-expr: num_visgroups
      - id: cordon_low
        type: point
      - id: cordon_high
        type: point
      - id: num_cameras
        type: s4
      - id: cameras
        type: camera
        repeat: expr
        repeat-expr: num_cameras
      - id: num_paths
        type: s4
      - id: paths
        type: path
        repeat: expr
        repeat-expr: num_paths
  group:
    seq:
      - id: id
        type: s4
      - id: parent_id
        type: s4
      - id: flags
        type: s4
      - id: num_objects
        type: s4
      - id: color
        type: color
  visgroup:
    seq:
      - id: name
        type: szp_str
      - id: id
        type: s4
      - id: color
        type: color
      - id: visible
        type: u1
  camera:
    seq:
      - id: position
        type: point
      - id: lookat
        type: point
      - id: flags
        type: s4
      - id: color
        type: color
  path:
    seq:
      - id: classname
        type: szp_str
      - id: name
        type: szp_str
      - id: direction
        type: s4
      - id: flags
        type: s4
      - id: color
        type: color
      - id: num_nodes
        type: s4
      - id: nodes
        type: path_node
        repeat: expr
        repeat-expr: num_nodes
  path_node:
    seq:
      - id: name_override
        type: szp_str
      - id: fire_on_pass
        type: szp_str
      - id: position
        type: point
      - id: angles
        type: point
      - id: flags
        type: s4
      - id: color
        type: color
      - id: num_keyvalues
        type: s4
      - id: keyvalues
        type: keyvalue
        repeat: expr
        repeat-expr: num_keyvalues
  entity:
    seq:
      - id: classname
        type: szp_str
      - id: origin
        type: point
      - id: flags
        type: u4
      - id: group_id
        type: u4
      - id: root_group_id
        type: u4
      - id: color
        type: color
      - id: hardcoded_properties
        type: szp_str
        repeat: expr
        repeat-expr: 13
      - id: spawnflags
        type: u4
      - id: angles
        size: point
      - id: rendering
        type: u4
      - id: fx_color
        type: color
      - id: render_mode
        type: u4
      - id: render_fx
        type: u4
      - id: body
        type: u2
      - id: skin
        type: u2
      - id: sequence
        type: u4
      - id: framerate
        type: f4
      - id: scale
        type: f4
      - id: radius
        type: f4
      - id: unknown
        size: 28
      - id: num_keyvalues
        type: s4
      - id: keyvalues
        type: keyvalue
        repeat: expr
        repeat-expr: num_keyvalues
      - id: num_visgroups
        type: s4
      - id: visgroups
        type: s4
        repeat: expr
        repeat-expr: num_visgroups
      - id: num_solids
        type: s4
      - id: solids
        type: solid
        repeat: expr
        repeat-expr: num_solids
  solid:
    seq:
      - id: num_patches
        type: s4
      - id: flags
        type: u4
      - id: group_id
        type: u4
      - id: root_group_id
        type: u4
      - id: color
        type: color
      - id: num_visgroups
        type: s4
      - id: visgroups
        type: s4
        repeat: expr
        repeat-expr: num_visgroups
      - id: num_faces
        type: s4
      - id: faces
        type: face
        repeat: expr
        repeat-expr: num_faces
      - id: patches
        type: patch
        repeat: expr
        repeat-expr: num_patches
  face:
    seq:
      - id: something
        type: s4
      - id: num_vertices
        type: s4
      - id: texture
        type: surface_properties
      - id: plane_normal
        type: point
      - id: plane_distance
        type: f4
      - id: something2
        type: u4
      - id: vertices
        type: vertex
        repeat: expr
        repeat-expr: num_vertices
  patch:
    seq:
      - id: width
        type: s4
      - id: height
        type: s4
      - id: texture
        type: surface_properties
      - id: something
        type: s4
      - id: points
        type: patch_point
        repeat: expr
        repeat-expr: 32 * 32
  patch_point:
    seq:
      - id: position
        type: point
      - id: normal
        type: point
      - id: texture_coordinate
        type: point
  surface_properties:
    seq:
      - id: x_axis
        type: point
      - id: x_shift
        type: f4
      - id: y_axis
        type: point
      - id: y_shift
        type: f4
      - id: x_scale
        type: f4
      - id: y_scale
        type: f4
      - id: rotation
        type: f4
      - id: something1
        type: s4
      - id: something2
        type: s4
      - id: something3
        type: s4
      - id: something4
        type: s4
      - id: flags
        type: s4
      - id: texture_name
        type: strz
        encoding: ASCII
        size: 64
  point:
    seq:
      - id: x
        type: f4
      - id: y
        type: f4
      - id: z
        type: f4
  vertex:
    seq:
      - id: texture_coordinate
        type: point
      - id: position
        type: point
  color:
    seq:
      - id: r
        type: u1
      - id: g
        type: u1
      - id: b
        type: u1
      - id: a
        type: u1
  szp_str:
    seq:
      - id: size
        type: s4
      - id: value
        type: strz
        size: size < 0 ? 0 : size
        encoding: ASCII
  keyvalue:
    seq:
      - id: key
        type: szp_str
      - id: value
        type: szp_str