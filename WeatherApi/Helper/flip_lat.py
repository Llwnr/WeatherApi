#!/usr/bin/env python
import json
import argparse
import sys
import numpy as np # type: ignore
import copy
import math # Import math for isnan check

def adjust_latitude_for_leaflet(grib_message):
    """
    Adjusts grib2json message for tools like Leaflet Velocity that expect
    la1 > la2 for North-to-South grids and a positive dy.

    Specifically:
    1. Swaps la1 and la2 if la1 < la2.
    2. Reverses the order of data rows if la1 < la2.
    3. Keeps dy as its original (usually positive) value.

    Args:
        grib_message (dict): A dictionary representing a single GRIB message
                             from grib2json output, containing 'header' and 'data'.

    Returns:
        dict: A new dictionary with potentially swapped latitudes and reversed data,
              suitable for libraries expecting la1 > la2 for N-S grids.

    Raises:
        ValueError: If header information is missing or inconsistent.
        TypeError: If input is not a dictionary.
        RuntimeError: If numpy operations fail.
    """
    if not isinstance(grib_message, dict):
        raise TypeError("Input must be a dictionary representing a grib message.")

    if 'header' not in grib_message or 'data' not in grib_message:
        raise ValueError("Input dictionary must contain 'header' and 'data' keys.")

    # Deep copy to avoid modifying the original object in place if needed elsewhere
    modified_message = copy.deepcopy(grib_message)
    header = modified_message['header']
    data = modified_message['data'] # Keep reference to original data list for now

    # --- 1. Extract and validate metadata ---
    try:
        la1_orig = header['la1'] # Latitude of first grid point
        la2_orig = header['la2'] # Latitude of last grid point
        nx = header['nx']        # Number of points along longitude
        ny = header['ny']        # Number of points along latitude
        dy_orig = header['dy']   # Latitude grid spacing (keep original sign, usually +)
    except KeyError as e:
        raise ValueError(f"Missing essential key in header: {e}")

    # Check numeric types, allowing None for data initially
    if not all(isinstance(val, (int, float)) for val in [la1_orig, la2_orig, nx, ny, dy_orig]):
         raise ValueError("nx, ny, la1, la2, dy must be numeric.")

    if nx <= 0 or ny <= 0:
        raise ValueError(f"Grid dimensions nx ({nx}) and ny ({ny}) must be positive.")

    expected_data_length = nx * ny
    actual_data_length = len(data) if isinstance(data, list) else 0
    if actual_data_length != expected_data_length:
        raise ValueError(
            f"Data length ({actual_data_length}) does not match nx * ny "
            f"({nx} * {ny} = {expected_data_length})."
        )

    # --- 2. Check if adjustment is needed (la1 should be > la2 for N->S) ---
    # If la1 is already greater than or equal to la2, the grid is likely already N->S
    # or has only one latitude row. No change needed in this case.
    if la1_orig >= la2_orig:
        # print(f"Message already has la1 ({la1_orig}) >= la2 ({la2_orig}). No latitude changes made.", file=sys.stderr)
        # Return the original (copied) message; rounding will happen in main()
        return modified_message

    print(f"Adjusting message (la1 {la1_orig} < la2 {la2_orig})...", file=sys.stdout)

    # --- 3. Modify Header (if la1_orig < la2_orig) ---
    # Swap latitudes to make la1 the northernmost latitude
    header['la1'] = la2_orig
    header['la2'] = la1_orig
    # IMPORTANT: Keep dy as its original value (don't negate).
    # Leaflet Velocity likely uses la1 > la2 to infer N->S scan
    # and expects dy to be the positive grid spacing.
    header['dy'] = dy_orig # Keep original dy

    # --- 4. Reorder Data (if la1_orig < la2_orig) ---
    if ny == 1:
        # No data reordering needed if only one latitude row
        print("Warning: Only one latitude point (ny=1). Data order remains unchanged.", file=sys.stderr)
        # Data is already a list, no need to reassign modified_message['data'] = data
    else:
        try:
            # Convert flat data list to 2D numpy array (ny rows, nx columns)
            # The original data corresponds to la1_orig -> la2_orig
            # Replace None with np.nan for numpy operations
            data_for_numpy = [np.nan if x is None else x for x in data]
            data_array = np.array(data_for_numpy).reshape((ny, nx))

            # Reverse the order of rows (latitude dimension) so it aligns
            # with the new header where la1 is the northernmost latitude.
            reversed_data_array = data_array[::-1, :]

            # Flatten the array back to a list in the new order
            # Convert np.nan back to None for JSON compatibility
            modified_message['data'] = [None if math.isnan(x) else x for x in reversed_data_array.flatten().tolist()]
        except Exception as e:
            # Catch potential errors during numpy operations (e.g., reshape)
            raise RuntimeError(f"Error processing data with numpy: {e}")

    # Rounding is now handled in the main function after this returns
    return modified_message

def main():
    parser = argparse.ArgumentParser(
        description="Adjusts grib2json output for libraries like Leaflet Velocity. "
                    "Ensures la1 > la2 for North-to-South grids by swapping la1/la2 "
                    "and reversing data rows if necessary, while keeping dy positive. "
                    "Also rounds all data values to 2 decimal places. "
                    "Outputs the most compact standard JSON by default (no indentation)."
    )
    parser.add_argument(
        "input_json",
        help="Path to the input JSON file (from grib2json)."
    )
    parser.add_argument(
        "output_json",
        help="Path to save the modified JSON file."
    )
    parser.add_argument(
        "-p", "--pretty",
        action="store_true",
        help="Output JSON with indentation for readability (increases file size)."
    )

    args = parser.parse_args()

    try:
        print(f"Reading input JSON: {args.input_json}", file=sys.stdout)
        with open(args.input_json, 'r') as f:
            grib_data_list = json.load(f)
    except FileNotFoundError:
        print(f"Error: Input file not found: {args.input_json}", file=sys.stderr)
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error decoding JSON from {args.input_json}: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"An unexpected error occurred while reading the input file: {e}", file=sys.stderr)
        sys.exit(1)

    if not isinstance(grib_data_list, list):
        print(f"Error: Expected input JSON to be a list of objects, but got {type(grib_data_list)}.", file=sys.stderr)
        sys.exit(1)

    processed_data = []
    print(f"Processing {len(grib_data_list)} GRIB message(s)...", file=sys.stdout)
    for i, message in enumerate(grib_data_list):
        processed_message = None # Initialize
        try:
            # Check if the message needs adjustment before processing
            needs_adjustment = False
            if 'header' in message and 'la1' in message['header'] and 'la2' in message['header']:
                 if message['header']['la1'] < message['header']['la2']:
                     needs_adjustment = True
                 # else: # la1 >= la2, assume it's already correct or doesn't need flipping
                     # print(f"Skipping adjustment for message {i} (la1 >= la2).", file=sys.stderr)
            else:
                 print(f"Warning: Skipping latitude adjustment check for message {i} due to missing header/latitude info.", file=sys.stderr)

            if needs_adjustment:
                # Adjustment potentially needed, call the function
                processed_message = adjust_latitude_for_leaflet(message)
            else:
                # No adjustment needed based on latitude, just copy the original
                # Use deepcopy to ensure the original list isn't modified by rounding later
                processed_message = copy.deepcopy(message)

            # --- NEW: Round data points to 2 decimal places ---
            if processed_message and 'data' in processed_message and isinstance(processed_message['data'], list):
                try:
                    # Use list comprehension for rounding. Handle None values explicitly.
                    rounded_data = []
                    for x in processed_message['data']:
                        if isinstance(x, (int, float)):
                            rounded_data.append(round(x, 2))
                        else:
                            # Keep non-numeric values (like None) as they are
                            rounded_data.append(x)
                    processed_message['data'] = rounded_data
                except TypeError as te:
                    print(f"Warning: Could not round data for message {i}, contains non-numeric types? Error: {te}", file=sys.stderr)
                except Exception as round_e:
                    print(f"An unexpected error occurred during data rounding for message {i}: {round_e}", file=sys.stderr)
                    # Depending on desired behavior, you might want to exit here or just continue
                    # sys.exit(1)

            # Append the processed (and potentially rounded) message
            if processed_message:
                processed_data.append(processed_message)
            else:
                 # This case should ideally not happen if input validation is good,
                 # but as a fallback, append the original (copied) message if processing failed somehow
                 print(f"Warning: Message {i} processing resulted in None, appending original.", file=sys.stderr)
                 processed_data.append(copy.deepcopy(message))


        except (ValueError, TypeError, RuntimeError) as e:
            print(f"Error processing message {i} during latitude adjustment: {e}", file=sys.stderr)
            # Exit on error during adjustment to ensure output integrity
            sys.exit(1)
        except Exception as e:
             print(f"An unexpected error occurred processing message {i}: {e}", file=sys.stderr)
             sys.exit(1)


    try:
        print(f"Writing output JSON: {args.output_json}", file=sys.stdout)
        with open(args.output_json, 'w') as f:
            # For maximum standard JSON compression, indent should be None.
            # The --pretty flag adds indentation for readability.
            indent_level = 2 if args.pretty else None
            # Using separators=(',', ':') removes spaces after commas and colons for max compactness
            json_separators = (',', ':') if not args.pretty else None
            json.dump(processed_data, f, indent=indent_level, separators=json_separators)

        print("Processing complete.", file=sys.stdout)
    except IOError as e:
        print(f"Error writing output file {args.output_json}: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"An unexpected error occurred while writing the output file: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()